using NguyenNhan_2179_tuan3.Models;

namespace NguyenNhan_2179_tuan3.Services;
public interface IVnPayService
{
    string CreatePaymentUrl(PaymentInformationModel model, HttpContext context);
    PaymentResponseModel PaymentExecute(IQueryCollection collections);
}