using Domain.Entities;

namespace WebApplication1.backend.Domain.Entities
{
    public class User
    {
        public Guid Id { get; init; }
        public string Name { get; private set; }
        public string Email { get; private set; }
        public string Cpf { get; private set; }
        public string PasswordHash { get; private set; }
        public UserRole Role { get; private set; }
    }
}
