using DomainLayer.Common;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace InfrastructureLayer.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        protected readonly SNMDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public GenericRepository(SNMDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public virtual async Task<T?> GetByIdAsync(Guid id)
        {
            var entity = await _dbSet.FindAsync(id);
            return entity is ISoftDelete { IsDeleted: true } ? null : entity;
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await ActiveQuery().ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return await ActiveQuery().Where(predicate).ToListAsync();
        }

        public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return await ActiveQuery().FirstOrDefaultAsync(predicate);
        }

        public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
        {
            return await ActiveQuery().AnyAsync(predicate);
        }

        public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
        {
            return predicate == null
                ? await ActiveQuery().CountAsync()
                : await ActiveQuery().CountAsync(predicate);
        }

        public virtual async Task<PagedResult<T>> GetPagedAsync(
            Expression<Func<T, bool>>? predicate,
            int page,
            int pageSize,
            Expression<Func<T, object>>? orderBy = null,
            bool ascending = true,
            CancellationToken cancellationToken = default)
        {
            IQueryable<T> query = ActiveQuery();

            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            if (orderBy != null)
            {
                query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
            }

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<T>(items, totalCount);
        }

        public virtual async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public virtual async Task AddRangeAsync(IEnumerable<T> entities)
        {
            await _dbSet.AddRangeAsync(entities);
        }

        public virtual void Update(T entity)
        {
            _dbSet.Update(entity);
        }

        public virtual void UpdateRange(IEnumerable<T> entities)
        {
            _dbSet.UpdateRange(entities);
        }

        public virtual void Delete(T entity)
        {
            if (entity is ISoftDelete softDeleteEntity)
            {
                softDeleteEntity.IsDeleted = true;
                _dbSet.Update(entity);
                return;
            }

            _dbSet.Remove(entity);
        }

        public virtual void DeleteRange(IEnumerable<T> entities)
        {
            var entityList = entities.ToList();
            var softDeleteEntities = entityList.OfType<ISoftDelete>().ToList();
            if (softDeleteEntities.Count == entityList.Count)
            {
                foreach (var entity in softDeleteEntities)
                {
                    entity.IsDeleted = true;
                }

                _dbSet.UpdateRange(entityList);
                return;
            }

            _dbSet.RemoveRange(entityList);
        }

        public virtual async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        protected IQueryable<T> ActiveQuery()
            => typeof(ISoftDelete).IsAssignableFrom(typeof(T))
                ? _dbSet.Where(entity => !EF.Property<bool>(entity, nameof(ISoftDelete.IsDeleted)))
                : _dbSet;
    }
}
