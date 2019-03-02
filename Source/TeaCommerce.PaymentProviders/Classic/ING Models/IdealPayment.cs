using System.Collections.Generic;
using Newtonsoft.Json;

namespace TeaCommerce.PaymentProviders.Classic
{
    internal class IdealPayment : Payment
    {
        private List<TransactionResul> idealTransactions = new List<TransactionResul>();
        public override List<TransactionResul> PaymentType => idealTransactions;

        public void AddIssuer(string issuer)
        {
            var transactionDetails = new IdealTransaction
            {
                PaymentMethodDetails = new PaymentMethodDetailsResult { IssuerId = issuer }
            };

            idealTransactions.Add(transactionDetails);            
        }
    }
}