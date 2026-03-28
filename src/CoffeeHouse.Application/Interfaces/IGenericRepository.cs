using CoffeeHouse.Application.Common;
using System.Linq.Expressions;

namespace CoffeeHouse.Application.Interfaces
{
    public interface IGenericRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(object id);

        Task<PagedResult<T>> GetAllPagedAsync(
            int pageNumber = 1,
            int pageSize = 10,
            Expression<Func<T, bool>>? filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            string includeProperties = "");

        Task AddAsync(T entity);
        Task AddRangeAsync(IEnumerable<T> entities);
        void Update(T entity);
        void Delete(T entity);
        void DeleteRange(IEnumerable<T> entities);
        Task<T?> GetFirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
        IQueryable<T> GetQueryable();
    }
}
