namespace BioGuard.Api.Services;

public class PaymentGatewayFactory
{
    private readonly StripePaymentGateway _stripe;
    private readonly MercadoPagoPaymentGateway _mercadoPago;

    public PaymentGatewayFactory(StripePaymentGateway stripe, MercadoPagoPaymentGateway mercadoPago)
    {
        _stripe = stripe;
        _mercadoPago = mercadoPago;
    }

    public virtual IPaymentGateway GetGateway(string metodoPago)
    {
        return metodoPago?.ToLowerInvariant() switch
        {
            "stripe" => _stripe,
            "mercadopago" or "mercado_pago" or "mercado pago" => _mercadoPago,
            _ => throw new ArgumentException($"Método de pago no soportado: {metodoPago}. Use 'stripe' o 'mercadopago'.")
        };
    }
}
