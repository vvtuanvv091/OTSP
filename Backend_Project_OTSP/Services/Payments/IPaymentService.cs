using System.Threading;
using System.Threading.Tasks;
using Backend_Project_OTSP.Dtos.Payment;

namespace Backend_Project_OTSP.Services.Payments;

public interface IPaymentService
{
    Task<StudentPaymentOverviewVm> GetStudentPaymentOverviewAsync(string userId, CancellationToken ct = default);
    Task<PaymentCheckoutVm?> GetPaymentCheckoutAsync(int paymentId, string userId, CancellationToken ct = default);
    Task<InvoiceDetailsDto?> GetInvoiceDetailsAsync(int paymentId, string userId, CancellationToken ct = default);
    Task<bool> CheckPaymentStatusAsync(int paymentId, string userId, CancellationToken ct = default);
    Task<bool> ConfirmManualPaymentMockAsync(int paymentId, string userId, CancellationToken ct = default);
    Task<bool> ProcessSePayWebhookAsync(SePayWebhookDto payload, CancellationToken ct = default);
    Task<SePayCheckResultDto> CheckSePayApiConnectionAsync(CancellationToken ct = default);
}