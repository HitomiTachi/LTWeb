using NguyenNhan_2179_tuan3.Models;

namespace NguyenNhan_2179_tuan3.Repositories
{
    public interface ICategoryRepository
    {
        Task<IEnumerable<Category>> GetAllAsync();
        Task<Category> GetByIdAsync(int id);
        Task AddAsync(Category category);
        Task<Category?> GetByIdWithProductsAsync(int id);
        Task UpdateAsync(Category category);
        Task DeleteAsync(int id);
    }
}
