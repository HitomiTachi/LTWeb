using System.ComponentModel.DataAnnotations;

namespace NguyenNhan_2179_tuan3.Models
{
    public class ProductImage
    {
        public int Id { get; set; }

        [Required]
        public string Url { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }
    }
}