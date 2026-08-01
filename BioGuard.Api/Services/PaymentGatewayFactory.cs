namespace BioGuard.Api.Services;

public class PaymentGatewayFactory
{
    private readonly StripePaymentGateway _stripe;

    public PaymentGatewayFactory(StripePaymentGateway stripe)
    {
        _stripe = stripe;
    }

    public virtual IPaymentGateway GetGateway(string? metodoPago)
    {
        var normalized = metodoPago?.ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized) || normalized == "stripe")
            return _stripe;

        throw new ArgumentException($"Método de pago no soportado: {metodoPago}. Solo se admite 'stripe'.");
    }
}
