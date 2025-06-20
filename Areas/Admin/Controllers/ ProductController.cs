using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NguyenNhan_2179_tuan3.Models;
using NguyenNhan_2179_tuan3.Repositories;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace NguyenNhan_2179_tuan3.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class ProductController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ApplicationDbContext _context;

        public ProductController(
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            ApplicationDbContext context)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _context = context;
        }

        // Hiển thị danh sách sản phẩm
        public async Task<IActionResult> IndexList()
        {
            var products = await _productRepository.GetAllAsync();
            return View(products);
        }

        // Hiển thị chi tiết sản phẩm
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id);
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

        // Xử lý thêm sản phẩm (nâng cấp: thêm nhiều ảnh phụ)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(Product product, IFormFile imageFile, List<IFormFile> Images)
        {
            if (ModelState.IsValid)
            {
                // 1. Xử lý ảnh đại diện (nếu có)
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

                // 2. Lưu sản phẩm trước để có ProductId
                await _productRepository.AddAsync(product);

                // 3. Xử lý lưu nhiều ảnh phụ (Images)
                if (Images != null && Images.Count > 0)
                {
                    var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    foreach (var file in Images)
                    {
                        if (file != null && file.Length > 0)
                        {
                            var ext = Path.GetExtension(file.FileName).ToLower();
                            if (!allowedExt.Contains(ext))
                                continue; // bỏ qua file không hợp lệ

                            var fileName = $"{Guid.NewGuid()}{ext}";
                            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
                            if (!Directory.Exists(folderPath))
                                Directory.CreateDirectory(folderPath);

                            var filePath = Path.Combine(folderPath, fileName);
                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }

                            // Lưu vào bảng ProductImages
                            var productImg = new ProductImage
                            {
                                Url = $"/images/{fileName}",
                                ProductId = product.Id
                            };
                            _context.ProductImages.Add(productImg);
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(IndexList));
            }

            ViewBag.Categories = await GetCategoriesSelectList();
            return View(product);
        }

        // Hiển thị form cập nhật sản phẩm (INCLUDE Images)
        public async Task<IActionResult> Update(int id)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            ViewBag.Categories = await GetCategoriesSelectList();
            return View(product);
        }

        // Xử lý cập nhật sản phẩm (nâng cấp: thêm nhiều ảnh phụ, cập nhật tồn kho)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, Product product, IFormFile imageFile, List<IFormFile> Images)
        {
            if (id != product.Id)
                return NotFound();

            // Lấy lại sản phẩm từ DB kèm ảnh phụ
            var oldProduct = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (oldProduct == null)
                return NotFound();

            ModelState.Remove("ImageUrl");

            if (ModelState.IsValid)
            {
                oldProduct.Name = product.Name;
                oldProduct.Description = product.Description;
                oldProduct.Price = product.Price;
                oldProduct.CategoryId = product.CategoryId;
                oldProduct.StockQuantity = product.StockQuantity; // ĐẢM BẢO CÓ DÒNG NÀY

                // Xử lý ảnh đại diện nếu upload mới
                if (imageFile != null && imageFile.Length > 0)
                {
                    var ext = Path.GetExtension(imageFile.FileName).ToLower();
                    var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    if (!allowedExt.Contains(ext))
                    {
                        ModelState.AddModelError("ImageUrl", "Chỉ cho phép định dạng ảnh JPG, PNG, GIF.");
                        product.ImageUrl = oldProduct.ImageUrl;
                        ViewBag.Categories = await GetCategoriesSelectList();
                        return View(oldProduct);
                    }

                    // Xóa ảnh đại diện cũ
                    if (!string.IsNullOrEmpty(oldProduct.ImageUrl))
                    {
                        var oldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", oldProduct.ImageUrl.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
                        if (System.IO.File.Exists(oldPath))
                            System.IO.File.Delete(oldPath);
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

                // Thêm ảnh phụ mới (nếu có)
                if (Images != null && Images.Count > 0)
                {
                    var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    foreach (var file in Images)
                    {
                        if (file != null && file.Length > 0)
                        {
                            var ext = Path.GetExtension(file.FileName).ToLower();
                            if (!allowedExt.Contains(ext))
                                continue;

                            var fileName = $"{Guid.NewGuid()}{ext}";
                            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
                            if (!Directory.Exists(folderPath))
                                Directory.CreateDirectory(folderPath);

                            var filePath = Path.Combine(folderPath, fileName);
                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }

                            var productImg = new ProductImage
                            {
                                Url = $"/images/{fileName}",
                                ProductId = oldProduct.Id
                            };
                            _context.ProductImages.Add(productImg);
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                await _productRepository.UpdateAsync(oldProduct);
                return RedirectToAction(nameof(IndexList));
            }

            product.ImageUrl = oldProduct.ImageUrl;
            product.Images = oldProduct.Images;
            ViewBag.Categories = await GetCategoriesSelectList();
            return View(product);
        }

        // Xóa ảnh phụ (AJAX)
        [HttpPost]
        public async Task<IActionResult> RemoveSubImage(int imgId)
        {
            var img = await _context.ProductImages.FindAsync(imgId);
            if (img != null)
            {
                var imgPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", img.Url.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (System.IO.File.Exists(imgPath))
                    System.IO.File.Delete(imgPath);

                _context.ProductImages.Remove(img);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        // Hiển thị form xóa sản phẩm
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (product == null)
                return NotFound();

            return View(product);
        }

        // Xử lý xóa sản phẩm
        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (product != null)
            {
                // Xóa file ảnh đại diện nếu có
                if (!string.IsNullOrEmpty(product.ImageUrl))
                {
                    var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", product.ImageUrl.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }

                // Xóa file các ảnh phụ nếu có
                foreach (var img in product.Images)
                {
                    var imgPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", img.Url.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
                    if (System.IO.File.Exists(imgPath))
                        System.IO.File.Delete(imgPath);
                }
                _context.ProductImages.RemoveRange(product.Images);
                await _context.SaveChangesAsync();

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