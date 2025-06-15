using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NguyenNhan_2179_tuan3.Models;
using NguyenNhan_2179_tuan3.Repositories;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NguyenNhan_2179_tuan3.Areas.Admin.Controllers
{

    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class ProductController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;

        public ProductController(IProductRepository productRepository, ICategoryRepository categoryRepository)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
        }

        // Hiển thị danh sách sản phẩm
        public async Task<IActionResult> IndexList()
        {
            var products = await _productRepository.GetAllAsync();
            return View(products);
        }

        // Hiển thị chi tiết sản phẩm
        public async Task<IActionResult> Display(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
                return NotFound();

            return View(product);
        }

        // Hiển thị form thêm sản phẩm
        public async Task<IActionResult> Add()
        {
            ViewBag.Categories = await GetCategoriesSelectList();
            return View();
        }

        // Xử lý thêm sản phẩm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(Product product, IFormFile imageFile)
        {
            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    var ext = Path.GetExtension(imageFile.FileName).ToLower();
                    var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    if (!allowedExt.Contains(ext))
                    {
                        ModelState.AddModelError("ImageUrl", "Chỉ cho phép định dạng ảnh JPG, PNG, GIF.");
                        ViewBag.Categories = await GetCategoriesSelectList();
                        return View(product);
                    }

                    var fileName = $"{Guid.NewGuid()}{ext}";
                    var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }
                    var filePath = Path.Combine(folderPath, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(stream);
                    }
                    product.ImageUrl = $"/images/{fileName}";
                }

                await _productRepository.AddAsync(product);
                return RedirectToAction(nameof(IndexList));
            }

            ViewBag.Categories = await GetCategoriesSelectList();
            return View(product);
        }

        // Hiển thị form cập nhật sản phẩm
        public async Task<IActionResult> Update(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
                return NotFound();

            ViewBag.Categories = await GetCategoriesSelectList();
            return View(product); // Ảnh cũ được giữ ở đây
        }

        // Xử lý cập nhật sản phẩm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, Product product, IFormFile imageFile)
        {
            if (id != product.Id)
                return NotFound();

            var oldProduct = await _productRepository.GetByIdAsync(id);
            if (oldProduct == null)
                return NotFound();

            // Bỏ validation bắt buộc ảnh mới
            ModelState.Remove("ImageUrl");

            if (ModelState.IsValid)
            {
                oldProduct.Name = product.Name;
                oldProduct.Description = product.Description;
                oldProduct.Price = product.Price;
                oldProduct.CategoryId = product.CategoryId;

                if (imageFile != null && imageFile.Length > 0)
                {
                    var ext = Path.GetExtension(imageFile.FileName).ToLower();
                    var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    if (!allowedExt.Contains(ext))
                    {
                        ModelState.AddModelError("ImageUrl", "Chỉ cho phép định dạng ảnh JPG, PNG, GIF.");
                        // Giữ ảnh cũ khi báo lỗi
                        product.ImageUrl = oldProduct.ImageUrl;
                        ViewBag.Categories = await GetCategoriesSelectList();
                        return View(product);
                    }

                    var fileName = $"{Guid.NewGuid()}{ext}";
                    var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }

                    var filePath = Path.Combine(folderPath, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(stream);
                    }

                    oldProduct.ImageUrl = $"/images/{fileName}";
                }
                else
                {
                    // Giữ nguyên ảnh cũ nếu không chọn ảnh mới
                    product.ImageUrl = oldProduct.ImageUrl;
                }

                await _productRepository.UpdateAsync(oldProduct);
                return RedirectToAction(nameof(IndexList));
            }

            // Model không hợp lệ thì giữ lại ảnh cũ và trả View
            product.ImageUrl = oldProduct.ImageUrl;
            ViewBag.Categories = await GetCategoriesSelectList();
            return View(product);
        }

        // Hiển thị form xóa sản phẩm
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
                return NotFound();

            return View(product);
        }

        // Xử lý xóa sản phẩm
        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product != null)
            {
                // Xóa file ảnh cũ nếu có
                if (!string.IsNullOrEmpty(product.ImageUrl))
                {
                    var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", product.ImageUrl.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }

                await _productRepository.DeleteAsync(id);
            }
            return RedirectToAction(nameof(IndexList));
        }

        // Helper method: Lấy danh sách danh mục dưới dạng SelectList
        private async Task<IEnumerable<SelectListItem>> GetCategoriesSelectList()
        {
            var categories = await _categoryRepository.GetAllAsync();
            return categories.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            });
        }
    }
}
