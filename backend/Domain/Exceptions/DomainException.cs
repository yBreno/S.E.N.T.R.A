namespace Domain.Exceptions;
public abstract class DomainException(string message) : Exception(message);
public class NotFoundException(string message) : DomainException(message);
public class BusinessRuleException(string message) : DomainException(message);
public class ConflictException(string message) : DomainException(message);
public class ForbiddenException(string message) : DomainException(message);
public class UnauthorizedException(string message) : DomainException(message);
