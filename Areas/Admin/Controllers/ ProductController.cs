using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NguyenNhan_2179_tuan3.Models;
using NguyenNhan_2179_tuan3.Repositories;
using System;
using System.Collections.Generic;
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

        public async Task<IActionResult> IndexList()
        {
            var products = await _productRepository.GetAllAsync();
            return View(products);
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (product == null) return NotFound();
            return View(product);
        }

        public async Task<IActionResult> Add()
        {
            ViewBag.Categories = await GetCategoriesSelectList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(Product product, IFormFile imageFile, List<IFormFile> Images)
        {
            if (ModelState.IsValid)
            {
                var imgUrl = await SaveImageAsync(imageFile);
                if (imageFile != null && imgUrl == null)
                {
                    ModelState.AddModelError("ImageUrl", "Chỉ cho phép định dạng ảnh JPG, PNG, GIF.");
                    ViewBag.Categories = await GetCategoriesSelectList();
                    return View(product);
                }

                product.ImageUrl = imgUrl;

                await _productRepository.AddAsync(product);

                if (Images != null && Images.Count > 0)
                {
                    foreach (var file in Images)
                    {
                        var subUrl = await SaveImageAsync(file);
                        if (subUrl != null)
                        {
                            _context.ProductImages.Add(new ProductImage
                            {
                                Url = subUrl,
                                ProductId = product.Id
                            });
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(IndexList));
            }

            ViewBag.Categories = await GetCategoriesSelectList();
            return View(product);
        }

        // GET: Admin/Product/Update/5
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

        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (product == null) return NotFound();

            return View(product);
        }

        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (product != null)
            {
                if (!string.IsNullOrEmpty(product.ImageUrl))
                {
                    var imagePath = Path.Combine("wwwroot", product.ImageUrl.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
                    if (System.IO.File.Exists(imagePath))
                        System.IO.File.Delete(imagePath);
                }

                foreach (var img in product.Images)
                {
                    var imgPath = Path.Combine("wwwroot", img.Url.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
                    if (System.IO.File.Exists(imgPath))
                        System.IO.File.Delete(imgPath);
                }

                _context.ProductImages.RemoveRange(product.Images);
                await _context.SaveChangesAsync();

                await _productRepository.DeleteAsync(id);
            }
            return RedirectToAction(nameof(IndexList));
        }

        private async Task<string?> SaveImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0) return null;

            var ext = Path.GetExtension(file.FileName).ToLower();
            var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            if (!allowedExt.Contains(ext)) return null;

            var fileName = $"{Guid.NewGuid()}{ext}";
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var filePath = Path.Combine(folderPath, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/images/{fileName}";
        }

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
