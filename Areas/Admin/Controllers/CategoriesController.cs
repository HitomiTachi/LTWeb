﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NguyenNhan_2179_tuan3.Models;
using NguyenNhan_2179_tuan3.Repositories;

[Area("Admin")]
[Authorize(Roles = SD.Role_Admin)]
public class CategoriesController : Controller
{

    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;

    public CategoriesController(IProductRepository productRepository,
        ICategoryRepository categoryRepository)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
    }

    public async Task<IActionResult> Index()
    {
        var category = await _categoryRepository.GetAllAsync();
        return View(category);
    }

    public async Task<IActionResult> Display(int id)
    {
        var category = await _categoryRepository.GetByIdAsync(id);
        if (category == null)
        {
            return NotFound();
        }
        return View(category);
    }

    public IActionResult Add()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Add(Category category)
    {
        if (ModelState.IsValid)
        {
            await _categoryRepository.AddAsync(category); // ✅ Thêm await
            return RedirectToAction(nameof(Index));
        }
        return View(category);
    }

    public async Task<IActionResult> Update(int id)
    {
        var category = await _categoryRepository.GetByIdAsync(id);
        if (category == null)
        {
            return NotFound();
        }
        return View(category);
    }

    [HttpPost]
    public async Task<IActionResult> Update(int id, Category category)
    {
        if (id != category.Id)
        {
            return NotFound();
        }
        if (ModelState.IsValid)
        {
            await _categoryRepository.UpdateAsync(category); // ✅ Thêm await
            return RedirectToAction(nameof(Index));
        }
        return View(category);
    }

    public async Task<IActionResult> Delete(int id)
    {
        var category = await _categoryRepository.GetByIdAsync(id);
        if (category == null)
        {
            return NotFound();
        }
        return View(category);
    }

    [HttpPost, ActionName("DeleteConfirmed")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var category = await _categoryRepository.GetByIdAsync(id);
        if (category != null)
        {
            await _categoryRepository.DeleteAsync(id); // ✅ Thêm await
        }
        return RedirectToAction(nameof(Index));
    }
}
