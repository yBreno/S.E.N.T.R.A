using Application.Common;
using Application.DTOs.Address;
using Application.DTOs.Agent;
using Application.DTOs.Auth;
using Application.DTOs.Equipment;
using Application.DTOs.Maintenance;
using Application.Security;
using Application.Services;
using Application.Validators;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Rules;
using FluentValidation;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Tests;

/// <summary>
/// Testa a matriz de transicoes permitidas e proibidas para os status dos equipamentos.
/// </summary>
public class RegrasDeTransicaoDeStatusDeEquipamentoTests
{
    [Theory]
    [InlineData(EquipmentStatus.NotInstalled, EquipmentStatus.Operational)]
    [InlineData(EquipmentStatus.NotInstalled, EquipmentStatus.Deactivated)]
    [InlineData(EquipmentStatus.NotInstalled, EquipmentStatus.Stolen)]
    [InlineData(EquipmentStatus.Operational, EquipmentStatus.Malfunctioning)]
    [InlineData(EquipmentStatus.Operational, EquipmentStatus.Deactivated)]
    [InlineData(EquipmentStatus.Operational, EquipmentStatus.Stolen)]
    [InlineData(EquipmentStatus.Malfunctioning, EquipmentStatus.UnderMaintenance)]
    [InlineData(EquipmentStatus.Malfunctioning, EquipmentStatus.Deactivated)]
    [InlineData(EquipmentStatus.Malfunctioning, EquipmentStatus.Stolen)]
    [InlineData(EquipmentStatus.UnderMaintenance, EquipmentStatus.Operational)]
    [InlineData(EquipmentStatus.UnderMaintenance, EquipmentStatus.Deactivated)]
    [InlineData(EquipmentStatus.Deactivated, EquipmentStatus.Operational)]
    public void Deve_permitir_transicao_quando_a_combinacao_de_status_for_autorizada(
        EquipmentStatus statusAtual,
        EquipmentStatus novoStatus)
    {
        Assert.True(EquipmentStatusRules.CanTransition(statusAtual, novoStatus));
    }

    [Theory]
    [InlineData(EquipmentStatus.NotInstalled, EquipmentStatus.Malfunctioning)]
    [InlineData(EquipmentStatus.Operational, EquipmentStatus.UnderMaintenance)]
    [InlineData(EquipmentStatus.Malfunctioning, EquipmentStatus.Operational)]
    [InlineData(EquipmentStatus.UnderMaintenance, EquipmentStatus.Stolen)]
    [InlineData(EquipmentStatus.Deactivated, EquipmentStatus.Stolen)]
    public void Deve_bloquear_transicao_quando_a_combinacao_de_status_nao_for_autorizada(
        EquipmentStatus statusAtual,
        EquipmentStatus novoStatus)
    {
        Assert.False(EquipmentStatusRules.CanTransition(statusAtual, novoStatus));
    }

    [Theory]
    [InlineData(EquipmentStatus.NotInstalled)]
    [InlineData(EquipmentStatus.Operational)]
    [InlineData(EquipmentStatus.Malfunctioning)]
    [InlineData(EquipmentStatus.UnderMaintenance)]
    [InlineData(EquipmentStatus.Deactivated)]
    [InlineData(EquipmentStatus.Stolen)]
    public void Deve_impedir_qualquer_transicao_quando_o_equipamento_ja_estiver_como_roubado(
        EquipmentStatus novoStatus)
    {
        Assert.False(EquipmentStatusRules.CanTransition(EquipmentStatus.Stolen, novoStatus));
    }
}

/// <summary>
/// Testa a validacao de documentos brasileiros e a protecao de dados pessoais por mascaramento.
/// </summary>
public class ValidacaoEMascaramentoDeDocumentosTests
{
    [Theory]
    [InlineData("529.982.247-25", true)]
    [InlineData("11111111111", false)]
    [InlineData("52998224726", false)]
    [InlineData("", false)]
    public void Deve_validar_cpf_de_acordo_com_os_digitos_verificadores(
        string cpf,
        bool resultadoEsperado)
    {
        Assert.Equal(resultadoEsperado, Documents.IsValidCpf(cpf));
    }

    [Theory]
    [InlineData("11.222.333/0001-81", true)]
    [InlineData("11.222.333/0001-82", false)]
    public void Deve_validar_cnpj_de_acordo_com_os_digitos_verificadores(
        string cnpj,
        bool resultadoEsperado)
    {
        Assert.Equal(resultadoEsperado, Documents.IsValidCnpj(cnpj));
    }

    [Theory]
    [InlineData("ABC-1234", true)]
    [InlineData("ABC1D23", true)]
    [InlineData("AB12345", false)]
    public void Deve_validar_placa_de_veiculo_nos_formatos_aceitos(
        string placa,
        bool resultadoEsperado)
    {
        Assert.Equal(resultadoEsperado, Documents.IsValidLicensePlate(placa));
    }

    [Fact]
    public void Deve_mascarar_o_cpf_para_nao_expor_todos_os_digitos_pessoais()
    {
        Assert.Equal("***.982.247-**", Documents.MaskCpf("52998224725"));
    }
}

/// <summary>
/// Infraestrutura comum dos testes de servico: cada teste recebe um banco InMemory isolado.
/// </summary>
public abstract class CenarioDeTestesDeServico : IDisposable
{
    protected readonly AppDbContext Banco = new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    protected AddressService Enderecos => new(
        new AddressRepository(Banco),
        Banco,
        new CreateAddressDtoValidator(),
        new UpdateAddressDtoValidator());

    protected AgentService Agentes => new(
        new AgentRepository(Banco),
        new UserRepository(Banco),
        Banco,
        new CreateAgentDtoValidator(),
        new UpdateAgentDtoValidator());

    protected EquipmentService Equipamentos => new(
        new EquipmentRepository(Banco),
        new AddressRepository(Banco),
        new AgentRepository(Banco),
        new AlertRepository(Banco),
        new MaintenanceRecordRepository(Banco),
        Banco,
        new CreateEquipmentDtoValidator(),
        new UpdateEquipmentDtoValidator(),
        new ChangeEquipmentStatusDtoValidator());

    protected MaintenanceService Manutencoes => new(
        new MaintenanceRecordRepository(Banco),
        new EquipmentRepository(Banco),
        new AgentRepository(Banco),
        new AlertRepository(Banco),
        Banco,
        new OpenMaintenanceDtoValidator(),
        new CloseMaintenanceDtoValidator());

    protected AuthService Autenticacao => new(
        new UserRepository(Banco),
        Banco,
        new BCryptPasswordHasher(),
        new JwtTokenService(Options.Create(new JwtSettings
        {
            Key = new string('k', 40),
            Issuer = "i",
            Audience = "a"
        })),
        new RegisterDtoValidator(),
        new LoginDtoValidator());

    protected static CreateAddressDto CriarEnderecoValido() => new()
    {
        Street = "Rua A",
        Neighborhood = "Centro",
        City = "Sao Paulo",
        State = States.SP,
        ZipCode = "01001-000",
        Latitude = -23.55,
        Longitude = -46.63
    };

    protected async Task<EquipmentResponseDto> CriarCameraValidaAsync(string numeroSerie = "SN-1")
    {
        var endereco = await Enderecos.CreateAsync(CriarEnderecoValido());

        return await Equipamentos.CreateAsync(new CreateEquipmentDto
        {
            Type = EquipmentType.Camera,
            SerialNumber = numeroSerie,
            Brand = "B",
            Model = "M",
            AddressId = endereco.Id
        });
    }

    public void Dispose()
    {
        Banco.Dispose();
    }
}

/// <summary>
/// Testa regras de cadastro e consistencia de enderecos, agentes e equipamentos (RD-01).
/// </summary>
public class ValidacaoDeCadastroDeEnderecoAgenteEEquipamentoTests : CenarioDeTestesDeServico
{
    [Fact]
    public async Task Deve_rejeitar_cadastro_de_endereco_quando_o_bairro_nao_for_informado()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            Enderecos.CreateAsync(CriarEnderecoValido() with { Neighborhood = "" }));
    }

    [Fact]
    public async Task Deve_rejeitar_cadastro_de_endereco_quando_a_latitude_ou_longitude_estiver_fora_dos_limites()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            Enderecos.CreateAsync(CriarEnderecoValido() with { Latitude = 91 }));

        await Assert.ThrowsAsync<ValidationException>(() =>
            Enderecos.CreateAsync(CriarEnderecoValido() with { Longitude = -181 }));
    }

    [Fact]
    public async Task Deve_rejeitar_agente_sem_bairro_e_coordenadas_validas_e_mascarar_seu_cpf_apos_cadastro_valido()
    {
        var dadosAgente = new CreateAgentDto
        {
            Name = "Ana",
            Cpf = "52998224725",
            BadgeNumber = "001",
            Role = AgentRole.TrafficAgent,
            Neighborhood = "",
            Latitude = 0,
            Longitude = 0
        };

        await Assert.ThrowsAsync<ValidationException>(() => Agentes.CreateAsync(dadosAgente));

        var agenteCriado = await Agentes.CreateAsync(dadosAgente with
        {
            Neighborhood = "Centro",
            Latitude = -23.5,
            Longitude = -46.6
        });

        Assert.Equal("***.982.247-**", agenteCriado.MaskedCpf);
    }

    [Fact]
    public async Task Deve_rejeitar_cadastro_de_equipamento_quando_o_endereco_informado_nao_existir()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Equipamentos.CreateAsync(new CreateEquipmentDto
            {
                Type = EquipmentType.Camera,
                SerialNumber = "X",
                Brand = "B",
                Model = "M",
                AddressId = Guid.NewGuid()
            }));
    }

    [Fact]
    public async Task Deve_impedir_cadastro_de_dois_equipamentos_com_o_mesmo_numero_de_serie()
    {
        var equipamentoExistente = await CriarCameraValidaAsync("DUP");

        await Assert.ThrowsAsync<ConflictException>(() =>
            Equipamentos.CreateAsync(new CreateEquipmentDto
            {
                Type = EquipmentType.Camera,
                SerialNumber = "DUP",
                Brand = "B",
                Model = "M",
                AddressId = equipamentoExistente.AddressId
            }));
    }
}

/// <summary>
/// Testa as transicoes de status, a abertura e o encerramento de manutencoes e os alertas associados.
/// </summary>
public class FluxoDeStatusManutencaoEAlertasDeEquipamentoTests : CenarioDeTestesDeServico
{
    [Fact]
    public async Task Deve_criar_equipamento_como_nao_instalado_e_rejeitar_mudanca_direta_para_com_defeito()
    {
        var equipamento = await CriarCameraValidaAsync();

        Assert.Equal(EquipmentStatus.NotInstalled, equipamento.Status);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Equipamentos.ChangeStatusAsync(
                equipamento.Id,
                new ChangeEquipmentStatusDto(EquipmentStatus.Malfunctioning, null),
                true));
    }

    [Fact]
    public async Task Deve_exigir_permissao_de_gestor_para_reativar_equipamento_desativado()
    {
        var equipamento = await CriarCameraValidaAsync();

        await Equipamentos.ChangeStatusAsync(
            equipamento.Id,
            new ChangeEquipmentStatusDto(EquipmentStatus.Deactivated, null),
            false);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Equipamentos.ChangeStatusAsync(
                equipamento.Id,
                new ChangeEquipmentStatusDto(EquipmentStatus.Operational, null),
                false));

        var equipamentoReativado = await Equipamentos.ChangeStatusAsync(
            equipamento.Id,
            new ChangeEquipmentStatusDto(EquipmentStatus.Operational, null),
            true);

        Assert.Equal(EquipmentStatus.Operational, equipamentoReativado.Status);
    }

    [Fact]
    public async Task Deve_impedir_reativacao_de_equipamento_apos_seu_status_ser_definido_como_roubado()
    {
        var equipamento = await CriarCameraValidaAsync();

        await Equipamentos.ChangeStatusAsync(
            equipamento.Id,
            new ChangeEquipmentStatusDto(EquipmentStatus.Stolen, null),
            true);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Equipamentos.ChangeStatusAsync(
                equipamento.Id,
                new ChangeEquipmentStatusDto(EquipmentStatus.Operational, null),
                true));
    }

    [Fact]
    public async Task Deve_gerar_alerta_abrir_manutencao_bloquear_sobreposicao_e_restaurar_operacao_ao_encerrar()
    {
        var equipamento = await CriarCameraValidaAsync();

        await Equipamentos.ChangeStatusAsync(
            equipamento.Id,
            new ChangeEquipmentStatusDto(EquipmentStatus.Operational, null),
            true);

        var equipamentoComDefeito = await Equipamentos.ChangeStatusAsync(
            equipamento.Id,
            new ChangeEquipmentStatusDto(EquipmentStatus.Malfunctioning, "sem sinal"),
            true);

        Assert.Equal(EquipmentStatus.Malfunctioning, equipamentoComDefeito.Status);
        Assert.Equal(MaintenanceNeed.Immediate, equipamentoComDefeito.MaintenanceNeed);

        var alertaAberto = Assert.Single(Banco.Alerts);
        Assert.Equal(AlertStatus.Open, alertaAberto.Status);

        var manutencaoAberta = await Manutencoes.OpenAsync(new OpenMaintenanceDto
        {
            EquipmentId = equipamento.Id,
            Description = "trocar modulo",
            PeriodicityDays = 30
        });

        Assert.Equal(
            EquipmentStatus.UnderMaintenance,
            (await Equipamentos.GetByIdAsync(equipamento.Id)).Status);

        await Assert.ThrowsAsync<ConflictException>(() =>
            Manutencoes.OpenAsync(new OpenMaintenanceDto
            {
                EquipmentId = equipamento.Id,
                Description = "outra"
            }));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Equipamentos.ChangeStatusAsync(
                equipamento.Id,
                new ChangeEquipmentStatusDto(EquipmentStatus.Operational, null),
                true));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Manutencoes.CloseAsync(
                manutencaoAberta.Id,
                new CloseMaintenanceDto { EndDate = manutencaoAberta.InitDate.AddDays(-1) }));

        var dataEncerramento = DateTime.UtcNow.AddHours(1);
        var manutencaoEncerrada = await Manutencoes.CloseAsync(
            manutencaoAberta.Id,
            new CloseMaintenanceDto { EndDate = dataEncerramento, Cost = 150m });

        Assert.Equal(MaintenanceStatus.Closed, manutencaoEncerrada.Status);

        var equipamentoAposManutencao = await Equipamentos.GetByIdAsync(equipamento.Id);
        Assert.Equal(EquipmentStatus.Operational, equipamentoAposManutencao.Status);
        Assert.Equal(dataEncerramento.AddDays(30), equipamentoAposManutencao.NextMaintenanceDate);
        Assert.Equal(MaintenanceNeed.Periodic, equipamentoAposManutencao.MaintenanceNeed);

        var alertaResolvido = Assert.Single(Banco.Alerts);
        Assert.Equal(AlertStatus.Resolved, alertaResolvido.Status);
        Assert.NotNull(alertaResolvido.ResolvedAt);
    }

    [Fact]
    public async Task Deve_rejeitar_abertura_de_manutencao_quando_o_equipamento_nao_estiver_com_defeito()
    {
        var equipamento = await CriarCameraValidaAsync();

        await Equipamentos.ChangeStatusAsync(
            equipamento.Id,
            new ChangeEquipmentStatusDto(EquipmentStatus.Operational, null),
            true);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Manutencoes.OpenAsync(new OpenMaintenanceDto
            {
                EquipmentId = equipamento.Id,
                Description = "inspecao"
            }));
    }
}

/// <summary>
/// Testa autenticacao, autorizacao de papeis, hash de senha e protecao de dados pessoais.
/// </summary>
public class AutenticacaoAutorizacaoESegurancaDeDadosTests : CenarioDeTestesDeServico
{
    [Fact]
    public async Task Deve_permitir_papel_elevado_apenas_para_administrador_e_nunca_expor_hash_ou_cpf_completo()
    {
        var dadosCadastro = new RegisterDto
        {
            Name = "Joao",
            Email = "Joao@Fogo.com",
            Cpf = "52998224725",
            Password = "Senha@123",
            Role = UserRole.OperationManager
        };

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Autenticacao.RegisterAsync(dadosCadastro, callerIsAdmin: false));

        var usuarioCriado = await Autenticacao.RegisterAsync(dadosCadastro, callerIsAdmin: true);

        Assert.Equal("joao@fogo.com", usuarioCriado.Email);
        Assert.DoesNotContain(
            typeof(UserResponseDto).GetProperties(),
            propriedade => propriedade.Name.Contains("Password") || propriedade.Name == "Cpf");
        Assert.NotEqual("Senha@123", Banco.Users.Single().PasswordHash);

        var resultadoLogin = await Autenticacao.LoginAsync(new LoginDto
        {
            Email = "joao@fogo.com",
            Password = "Senha@123"
        });

        Assert.False(string.IsNullOrEmpty(resultadoLogin.Token));

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            Autenticacao.LoginAsync(new LoginDto
            {
                Email = "joao@fogo.com",
                Password = "errada"
            }));
    }

    [Fact]
    public async Task Deve_rejeitar_senha_fraca_e_impedir_cadastro_com_email_ja_utilizado()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            Autenticacao.RegisterAsync(new RegisterDto
            {
                Name = "A",
                Email = "a@a.com",
                Cpf = "52998224725",
                Password = "fraca"
            }, false));

        var dadosCadastroValido = new RegisterDto
        {
            Name = "Ana",
            Email = "a@a.com",
            Cpf = "52998224725",
            Password = "Forte@1234"
        };

        await Autenticacao.RegisterAsync(dadosCadastroValido, false);

        await Assert.ThrowsAsync<ConflictException>(() =>
            Autenticacao.RegisterAsync(
                dadosCadastroValido with { Cpf = "11144477735" },
                false));
    }
}