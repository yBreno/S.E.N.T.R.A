using Application.Common;
using Application.DTOs.Auth;
using Application.Interfaces.IServices;
using Application.Interfaces.Security;
using Application.Mappings;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.IRepositories;
using FluentValidation;
namespace Application.Services;

public class AuthService(IUserRepository users, IUnitOfWork uow, IPasswordHasher hasher, ITokenService tokens,
    IValidator<RegisterDto> rv, IValidator<LoginDto> lv) : IAuthService
{
    public async Task<UserResponseDto> RegisterAsync(RegisterDto dto, bool callerIsAdmin, CancellationToken ct = default)
    {
        await rv.ValidateAndThrowAsync(dto, ct);

        var role = dto.Role ?? UserRole.Agent;
        if (role != UserRole.Agent && !callerIsAdmin)
            throw new ForbiddenException("Somente um Admin pode criar usuarios com papel elevado.");

        if (await users.EmailExistsAsync(dto.Email, ct)) throw new ConflictException("E-mail ja cadastrado.");
        if (await users.CpfExistsAsync(Documents.Digits(dto.Cpf), ct)) throw new ConflictException("CPF ja cadastrado.");

        var user = User.Create(dto.Name, dto.Email, dto.Cpf, hasher.Hash(dto.Password), role);
        await users.AddAsync(user, ct);
        await uow.SaveChangesAsync(ct);
        return user.ToResponse();
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        await lv.ValidateAndThrowAsync(dto, ct);
        var user = await users.GetByEmailAsync(dto.Email, ct);
        if (user is null || !hasher.Verify(dto.Password, user.PasswordHash))
            throw new UnauthorizedException("Credenciais invalidas.");
        var token = tokens.GenerateToken(user);
        return new AuthResponseDto(token.Token, token.ExpiresAt, user.ToResponse());
    }
}
