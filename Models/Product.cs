using System.ComponentModel.DataAnnotations;

namespace NguyenNhan_2179_tuan3.Models
{
    public class Product
    {
        public int Id { get; set; }
        [Required, StringLength(100)]
        public string Name { get; set; }

        [Range(0.01, 10000000.00, ErrorMessage = "Giá phải nằm trong khoảng từ 0,01 đến 10.000.000.")]
        public decimal Price { get; set; }

        public string Description { get; set; }
        public string? ImageUrl { get; set; }
        public List<ProductImage>? Images { get; set; }
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Số lượng tồn phải >= 0")]
        public int StockQuantity { get; set; }
    }
}
