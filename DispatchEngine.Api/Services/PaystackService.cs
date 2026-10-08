namespace DispatchEngine.Api.Services;
public interface IPaystackService {
    Task<string> InitiateTransfer(decimal amount, string accountNumber, string bankCode, string reference);
}
public class PaystackService : IPaystackService {
    public async Task<string> InitiateTransfer(decimal amount, string accountNumber, string bankCode, string reference) {
        // TODO: Real Paystack API - for now mock success for testing
        // Real code:
        // var client = new HttpClient();
        // client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "sk_test_xxx");
        // var body = { type: "nuban", name: "Rider", account_number: accountNumber, bank_code: bankCode, amount: amount*100, reference }
        // POST https://api.paystack.co/transfer
        await Task.Delay(500); // simulate network
        if (amount > 20000) throw new Exception("Paystack: Transfer amount exceeds test limit");
        return $"PAYSTACK_{Guid.NewGuid().ToString().Substring(0,8)}";
    }
}
