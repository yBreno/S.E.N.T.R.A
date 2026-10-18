using Domain.Entities;
using Domain.IRepositories;
using Infrastructure.Data;
namespace Infrastructure.Repositories;
public class OperationalBaseRepository(AppDbContext context) : Repository<OperationalBase>(context), IOperationalBaseRepository { }
