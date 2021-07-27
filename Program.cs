using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ZetiTest.Controllers;

namespace ZetiTest.VehicleBillingClient {
    class Program {
        //Client Code Fields for async calls to OpenAPI
        private static HttpClient _client = HttpClientFactory.Create();       
        public static CancellationToken _cancellationToken = new CancellationToken();
        private static string _startDate = "2021-02-01T00:00:00+00:00";
        private static string _endDate = "2021-02-28T23:59:00Z";

        //Customer specific Billing Fields
        private static string _assetOperator = "Bob's Taxis";
        private static double _billTotal = 0.00;
        // Feed-data is in Meters. Conversion rate  => Miles
        private static double _metersToMiles = 1609.34;
        // In £ per mile
        private static double _ratePerMile = 0.207;

        static void Main(string[] args) {
            GetVehicleBillingAsync();
        }

        static async void GetVehicleBillingAsync() {
            /* 
             * I modified generated OpenAPIClient to return ICollection<T> instead of Task Void
             * Also I included the types I needed inside 
             * VehicleTest\Controllers\BillingSwaggerClient.cs
             */
            BillingSwaggerClient billingClient = new BillingSwaggerClient(_client);
            var requestVehiclesAll = billingClient.VehiclesAsync(_cancellationToken).Result;
            var requestHistoryStart = billingClient.HistoryAsync(_startDate, _cancellationToken).Result;
            var requestHistoryEnd = billingClient.HistoryAsync(_endDate, _cancellationToken).Result;

            await Task.FromResult(requestVehiclesAll);
            await Task.FromResult(requestHistoryStart);
            await Task.FromResult(requestHistoryEnd);

            var mergedList = requestHistoryStart.Union(requestHistoryEnd).ToList();

            GenerateBill(billingClient, requestVehiclesAll, requestHistoryStart, requestHistoryEnd);
        }

        static void GenerateBill(BillingSwaggerClient billingClient, ICollection<Vehicle> fleet, ICollection<VehicleHistory> historyStart, ICollection<VehicleHistory> historyEnd) {

            try
            {
                // This step for clarity and readability
                ICollection<Vehicle> fleetAllList = fleet;
                ICollection<VehicleHistory> historyStartList = historyStart;
                ICollection<VehicleHistory> historyEndList = historyEnd;

                //Setup Initial runtime vars
                var mileageStart = 000000.0;
                var mileageEnd = 099999.0;
                var vin = "";

                //Bring List startDate and EndDate into one list for iteration
                var mergedList = historyStartList.Union(historyEndList).ToList();
                //Used for matching up Lisence plates for mileage calcs and Invoice creation
                var tempList = new List<VehicleHistory>();

                foreach (var vehicle in fleet)
                {
                    vin = vehicle.LicensePlate;
                    foreach (var record in mergedList)
                    {
                        if (record.LicensePlate.Equals(vin))
                        {
                            tempList.Add(record);
                        };
                    }

                    mileageStart = tempList[0].State.OdometerInMeters;
                    mileageEnd = tempList[1].State.OdometerInMeters;
                    CalculateCost(vin, mileageStart, mileageEnd);
                    tempList.Clear();
                    Console.WriteLine("Asset:" + vin);
                }
                GenerateInvoice(_billTotal);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }           
        }

        /* This section builds up a JSON object with relevant detail for Invoice
         * Called in GenerateInvoice() 
        */
        static JObject itemizedBill = new();
        public static void CalculateCost(string id, double start, double stop) {
            var chargePerVehicle = (stop - start) / _metersToMiles * _ratePerMile;
            itemizedBill.Add("Asset:" + id, "OStart/metres:" + start);
            itemizedBill.Add("Asset1:" + id, "OStop/metres:" + stop);
            itemizedBill.Add("DistanceMetres:" + id, (stop - start));
            itemizedBill.Add("DistanceMiles:" + id, (stop - start) / _metersToMiles);
            itemizedBill.Add("ChargePerVehicle:" + id, Math.Round(chargePerVehicle, 2));
            _billTotal += Math.Round(chargePerVehicle, 2);
        }

        //Generate JSON Bill statement    
        public static JObject GenerateInvoice(double total) {
            //To £ from p

            JObject jBill = JObject.FromObject(new
            {
                bill = new
                {
                    title = "Invoice " + _assetOperator.ToString(),
                    subtitle = "Generated on  " + DateTime.Now.ToLongDateString(),
                    link = "http://www.zetiorg.com",
                    detail = itemizedBill,
                    description = "Rate per Mile in £: " + _ratePerMile,
                    summary = "Total Due in £: " + Math.Round(total, 2)
                }
            });
            Console.WriteLine("Json Bill generated.");
            Console.WriteLine("See preview below:");
            Console.WriteLine("___-----------___");
            Console.Write(jBill);
            return jBill;
        }
    }
}
