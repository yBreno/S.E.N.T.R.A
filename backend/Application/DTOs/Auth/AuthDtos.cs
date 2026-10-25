using Domain.Enums;
namespace Application.DTOs.Auth;

public record RegisterDto
{
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Cpf { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public UserRole? Role { get; init; }
}
public record LoginDto
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
public record UserResponseDto(Guid Id, string Name, string Email, string MaskedCpf, UserRole Role);
public record AuthResponseDto(string Token, DateTime ExpiresAt, UserResponseDto User);
