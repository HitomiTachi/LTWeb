using System;
using System.Collections.Generic;
using System.Linq;

namespace NguyenNhan_2179_tuan3.Models
{
    public class ShoppingCart
    {
        public List<CartItem> Items { get; set; } = new List<CartItem>();

        public void AddItem(CartItem item)
        {
            var existingItem = Items.FirstOrDefault(i => i.ProductId == item.ProductId);
            if (existingItem != null)
            {
                existingItem.Quantity += item.Quantity;
            }
            else
            {
                Items.Add(item);
            }
        }

        public void RemoveItem(int productId)
        {
            Items.RemoveAll(i => i.ProductId == productId);
        }

        public void UpdateQuantity(int productId, int newQuantity)
        {
            var item = Items.FirstOrDefault(i => i.ProductId == productId);
            if (item != null && newQuantity > 0)
            {
                item.Quantity = newQuantity;
            }
            else if (item != null && newQuantity == 0)
            {
                Items.Remove(item);
            }
        }

        public decimal TotalPrice => Items.Sum(i => i.Price * i.Quantity);
    }
}
