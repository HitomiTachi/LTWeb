// Extensions/Cookie.cs
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace NguyenNhan_2179_tuan3.Extensions
{
    public static class Cookie
    {
        public static void SetObjectAsJson(this IResponseCookies cookies, string key, object value, int expireDays = 30)
        {
            var options = new CookieOptions
            {
                Expires = DateTime.Now.AddDays(expireDays),
                IsEssential = true,
                HttpOnly = false
            };
            cookies.Append(key, JsonSerializer.Serialize(value), options);
        }

        public static T GetObjectFromJson<T>(this IRequestCookieCollection cookies, string key)
        {
            cookies.TryGetValue(key, out string value);
            return string.IsNullOrEmpty(value) ? default : JsonSerializer.Deserialize<T>(value);
        }
    }
}
