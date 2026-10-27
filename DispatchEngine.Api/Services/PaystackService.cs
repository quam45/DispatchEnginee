namespace DispatchEngine.Api.Services;
public interface IPaystackService {
    Task<string> InitiateTransfer(decimal amount, string accountNumber, string bankCode, string reference);
}

// Thrown only when Paystack definitively rejected the transfer, i.e. no money moved and it is safe
// to refund. Timeouts and network errors are NOT this exception: the transfer may have gone through.
public class PaystackTransferException : Exception {
    public PaystackTransferException(string message) : base(message) { }
}

public class PaystackService : IPaystackService {
    public async Task<string> InitiateTransfer(decimal amount, string accountNumber, string bankCode, string reference) {
        // TODO: Real Paystack API - for now mock success for testing
        // Real code:
        // client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config["Paystack:SecretKey"]);
        // var body = { source: "balance", amount: amount*100, recipient: <recipient_code>, reference }
        // POST https://api.paystack.co/transfer  (map a 4xx "status": false response to PaystackTransferException)
        await Task.Delay(500); // simulate network
        if (amount > 20000) throw new PaystackTransferException("Transfer amount exceeds test limit");
        return $"PAYSTACK_{Guid.NewGuid().ToString().Substring(0,8)}";
    }
}
