using System.ComponentModel.DataAnnotations;

namespace NguyenNhan_2179_tuan3.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên danh mục là bắt buộc")]
        [StringLength(50, ErrorMessage = "Tên danh mục tối đa 50 ký tự")]
        public string Name { get; set; }

        // Không bắt buộc nhưng nên khởi tạo để tránh null
        public List<Product> Products { get; set; } = new List<Product>();

    }
}
