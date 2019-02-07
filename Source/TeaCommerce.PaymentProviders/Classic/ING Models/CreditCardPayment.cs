using System.Collections.Generic;

namespace TeaCommerce.PaymentProviders.Classic
{
    internal class CreditCardPayment : Payment
    {
        public override List<TransactionResul> PaymentType => new List<TransactionResul> { new TransactionResul { PaymentMethod = "credit-card" } };
    }
}