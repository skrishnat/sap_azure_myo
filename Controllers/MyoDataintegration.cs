using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Logging;
using RestSharp;
using System;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace sap_azure_myo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MyoDataintegration : ControllerBase
    {
        // GET api/user/firstname/lastname/address
        //string fromDate, string toDate, string option
        //("fromDate={fromDate}&toDate={toDate}&option={option}")


        [HttpGet("myo_sap")]
        public async Task<IActionResult> AsyncGet([FromQuery] MyDataIntegration direq)
        {
            var xmlDoc = new XmlDocument();
            var tmfDoc = new XmlDocument();
            string fromDate = direq.FromDate;
            string toDate =direq.ToDate;
            string option = direq.Option;
            
            var sapResponnse =  GetSapResponse(fromDate, toDate, option);
            xmlDoc.LoadXml(sapResponnse);
            var start = xmlDoc.InnerXml.IndexOf("<EtTable1>") + 10;
            var end = xmlDoc.InnerXml.IndexOf("</EtTable1>") - start;
            var ded = xmlDoc.InnerXml.Substring(start, end);
            tmfDoc.LoadXml("<root>\r\n" +
                     ded +
              "</root>\r\n");
            var sapJson = ConvertToCSV(tmfDoc);
            var result = POSTProcessBlobStorage(sapJson);
            return new OkObjectResult(result);
        }

        private string GetSapResponse(string fromdate, string todate, string option)
        {
            try
            {
                var client = new RestClient("http://RENOIHNPD01FO.Eichergroup.com:8000/sap/bc/srt/rfc/sap/zws_sales_planned_ord_details/900/zws_sales_planned_ord_details/zws_sales_planned_ord_details");
                client.Timeout = -1;
                var request = new RestRequest(Method.POST);
                request.AddHeader("Content-Type", "text/xml");
                request.AddHeader("Authorization", "Basic WlpBWlVSRTp3ZWxjb21lQDE=");
                request.AddParameter("text/xml", "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">\r\n  <soap:Body>\r\n<n0:ZsdpSalesPlannedOrdDetails xmlns:n0=\"urn:sap-com:document:sap:soap:functions:mc-style\">\r\n <ItDate>\r\n  <item>\r\n   <Sign>I</Sign>\r\n   <Option>EQ</Option>\r\n   <Low>2020-09-11</Low>\r\n   <High></High>\r\n  </item>\r\n </ItDate>\r\n</n0:ZsdpSalesPlannedOrdDetails>\r\n  </soap:Body>\r\n</soap:Envelope>\r\n", ParameterType.RequestBody);
                IRestResponse response = client.Execute(request);
                Console.WriteLine(response.Content);
                return response.Content;
            }
            catch (Exception e)


            {
                return e.Message;
            }
        }
        private static async Task<string> POSTProcessBlobStorage(String requestBody)
        {
            var dt = DateTime.Now;
            string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=redatalaketest;AccountKey=6TTFP3QB4eGIXjmZIAQxExCpOGj8Ql15pPEV5Rm0MVz9PwRFptjk4m24hDx8wbuSLfYO61guvkA4MV0nGBCt2Q==;EndpointSuffix=core.windows.net";
            // Check whether the connection string can be parsed.
            string result;
            if (Microsoft.Azure.Storage.CloudStorageAccount.TryParse(storageConnectionString, out CloudStorageAccount storageAccount))
            {
                // If the connection string is valid, proceed with operations against Blob storage here.

                // Create the CloudBlobClient that represents the Blob storage endpoint for the storage account.
                CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();

                // Create a incoming container.
                string foldercontainer = "200-sap";
                CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(foldercontainer);
                //await cloudBlobContainer.CreateAsync();
                await cloudBlobContainer.CreateIfNotExistsAsync();


                // Get a reference to the blob address, then upload the file to the blob.

                CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference("SalesPlannedOrderDetails" + dt.ToString("dd_MM_yyyy") + ".csv"); // "-"+ Guid.NewGuid().ToString());

                // Set the permissions so the blobs are public.
                BlobContainerPermissions permissions = new BlobContainerPermissions
                {
                    PublicAccess = BlobContainerPublicAccessType.Off
                };
                await cloudBlobContainer.SetPermissionsAsync(permissions);
                //await cloudBlobContainerout.SetPermissionsAsync(permissions);

                // await cloudBlockBlob.DeleteIfExistsAsync();
                await cloudBlockBlob.UploadTextAsync(requestBody);
                //await cloudBlockBlob.SnapshotAsync();

                Console.WriteLine("Data uploaded to RE Storage...");

                result = "RE Middleware Service, Function App : Success"; //+ keyValues;

            }
            else
            {
                // Otherwise, let the user know that they need to define the environment variable.
                Console.WriteLine("A connection string has not been defined in the system.");
                Console.WriteLine("Data could not be uploaded to RE Storage...");
                Console.ReadLine();
                result = null;
            }

            return result;
        }




        private static string ConvertToCSV(XmlDocument jsonContent)
        {

            StringReader theReader = new StringReader(jsonContent.InnerXml);
            DataSet theDataSet = new DataSet();
            theDataSet.ReadXml(theReader);

            StringBuilder content = new StringBuilder();

            if (theDataSet.Tables.Count >= 1)
            {
                DataTable table = theDataSet.Tables[0];

                if (table.Rows.Count > 0)
                {
                    DataRow dr1 = (DataRow)table.Rows[0];
                    int intColumnCount = dr1.Table.Columns.Count;
                    int index = 1;

                    //add column names
                    foreach (DataColumn item in dr1.Table.Columns)
                    {
                        content.Append(String.Format("\"{0}\"", item.ColumnName));
                        if (index < intColumnCount)
                            content.Append(",");
                        else
                            content.Append("\r\n");
                        index++;
                    }

                    //add column data
                    foreach (DataRow currentRow in table.Rows)
                    {
                        string strRow = string.Empty;
                        for (int y = 0; y <= intColumnCount - 1; y++)
                        {
                            strRow += "\"" + currentRow[y].ToString() + "\"";

                            if (y < intColumnCount - 1 && y >= 0)
                                strRow += ",";
                        }
                        content.Append(strRow + "\r\n");
                    }
                }

            }

            return content.ToString();
        }
    }
}
