using CoffeeHouse.Application.Common;
using CoffeeHouse.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CoffeeHouse.Infrastructure.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        protected readonly ApplicationDbContext _context;
        internal DbSet<T> dbSet;

        public GenericRepository(ApplicationDbContext context)
        {
            _context = context;
            dbSet = context.Set<T>();
        }

        public async Task<T?> GetByIdAsync(object id)
        {
            return await dbSet.FindAsync(id);
        }

        public async Task<PagedResult<T>> GetAllPagedAsync(
            int pageIndex = 1,
            int pageSize = 10,
            Expression<Func<T, bool>>? filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            string includeProperties = "")
        {
            IQueryable<T> query = dbSet;

            // 1. Áp dụng điều kiện lọc (nếu có)
            if (filter != null)
            {
                query = query.Where(filter);
            }

            // 2. Áp dụng Include các bảng liên quan (VD: Include Category vào Product)
            foreach (var includeProperty in includeProperties.Split
                (new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                query = query.Include(includeProperty);
            }

            // 3. THỰC HIỆN SẮP XẾP TẠI ĐÂY (RẤT QUAN TRỌNG)
            if (orderBy != null)
            {
                query = orderBy(query);
            }

            // 4. Đếm tổng số bản ghi trước khi phân trang (cho Vue3 biết tổng số trang)
            var totalCount = await query.CountAsync();

            // 5. Phân trang (Skip và Take)
            var items = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<T>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageIndex,
                PageSize = pageSize
            };
        }

        public async Task AddAsync(T entity) => await dbSet.AddAsync(entity);
        public async Task AddRangeAsync(IEnumerable<T> entities) => await dbSet.AddRangeAsync(entities);
        public void Update(T entity) => dbSet.Update(entity);
        public void Delete(T entity) => dbSet.Remove(entity);
        public void DeleteRange(IEnumerable<T> entities) => dbSet.RemoveRange(entities);
        public async Task<T?> GetFirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return await dbSet.FirstOrDefaultAsync(predicate);
        }

        public IQueryable<T> GetQueryable()
        {
            // _dbSet hoặc _context.Set<T>() tùy vào cách mi đặt tên biến ở trên
            return _context.Set<T>().AsQueryable();
        }
    }
}
