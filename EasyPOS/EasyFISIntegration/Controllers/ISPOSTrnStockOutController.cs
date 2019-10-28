﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace EasyPOS.EasyFISIntegration.Controllers
{
    class ISPOSTrnStockOutController
    {
        // ====
        // Data
        // ====
        private Data.easyposdbDataContext posdb = new Data.easyposdbDataContext(Modules.SysConnectionStringModule.GetConnectionString());

        public Forms.Software.SysSettings.SysSettingsForm sysSettingsForm;
        public String activityDate;

        // ===========
        // Constructor
        // ===========
        public ISPOSTrnStockOutController(Forms.Software.SysSettings.SysSettingsForm form, String actDate)
        {
            sysSettingsForm = form;
            activityDate = actDate;
        }

        // ===================
        // Fill Leading Zeroes
        // ===================
        public String FillLeadingZeroes(Int32 number, Int32 length)
        {
            var result = number.ToString();
            var pad = length - result.Length;
            while (pad > 0)
            {
                result = '0' + result;
                pad--;
            }

            return result;
        }

        // ==============
        // Sync Stock Out
        // ==============
        public async void SyncStockOut(String apiUrlHost, String branchCode)
        {
            await GetStockOut(apiUrlHost, branchCode);
        }

        // =============
        // Get Stock Out
        // =============
        public Task GetStockOut(String apiUrlHost, String branchCode)
        {
            try
            {
                DateTime dateTimeToday = DateTime.Now;
                String stockOutDate = Convert.ToDateTime(activityDate).ToString("MM-dd-yyyy", CultureInfo.InvariantCulture);

                // ============
                // Http Request
                // ============
                var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://" + apiUrlHost + "/api/get/POSIntegration/stockOut/" + stockOutDate + "/" + branchCode);
                httpWebRequest.Method = "GET";
                httpWebRequest.Accept = "application/json";

                // ================
                // Process Response
                // ================
                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    List<Entities.ISPOSTrnStockOut> stockOuts = (List<Entities.ISPOSTrnStockOut>)js.Deserialize(result, typeof(List<Entities.ISPOSTrnStockOut>));

                    if (stockOuts.Any())
                    {
                        foreach (var stockOut in stockOuts)
                        {
                            var currentStockOut = from d in posdb.TrnStockOuts where d.Remarks.Equals("OT-" + stockOut.BranchCode + "-" + stockOut.OTNumber) && d.TrnStockOutLines.Count() > 0 && d.IsLocked == true select d;
                            if (!currentStockOut.Any())
                            {
                                sysSettingsForm.logMessages("Saving Stock Out: OT-" + stockOut.BranchCode + "-" + stockOut.OTNumber + "\r\n\n");

                                var defaultPeriod = from d in posdb.MstPeriods select d;
                                var defaultSettings = from d in posdb.IntCloudSettings select d;

                                var lastStockOutNumber = from d in posdb.TrnStockOuts.OrderByDescending(d => d.Id) select d;
                                var stockOutNumberResult = defaultPeriod.FirstOrDefault().Period + "-000001";

                                if (lastStockOutNumber.Any())
                                {
                                    var stockOutNumberSplitStrings = lastStockOutNumber.FirstOrDefault().StockOutNumber;
                                    Int32 secondIndex = stockOutNumberSplitStrings.IndexOf('-', stockOutNumberSplitStrings.IndexOf('-'));
                                    var stockOutNumberSplitStringValue = stockOutNumberSplitStrings.Substring(secondIndex + 1);
                                    var stockOutNumber = Convert.ToInt32(stockOutNumberSplitStringValue) + 000001;
                                    stockOutNumberResult = defaultPeriod.FirstOrDefault().Period + "-" + FillLeadingZeroes(stockOutNumber, 6);
                                }

                                var accounts = from d in posdb.MstAccounts select d;
                                if (accounts.Any())
                                {
                                    Data.TrnStockOut newStockOut = new Data.TrnStockOut
                                    {
                                        PeriodId = defaultPeriod.FirstOrDefault().Id,
                                        StockOutDate = Convert.ToDateTime(stockOut.OTDate),
                                        StockOutNumber = stockOutNumberResult,
                                        AccountId = accounts.FirstOrDefault().Id,
                                        Remarks = "OT-" + stockOut.BranchCode + "-" + stockOut.OTNumber,
                                        PreparedBy = defaultSettings.FirstOrDefault().PostUserId,
                                        CheckedBy = defaultSettings.FirstOrDefault().PostUserId,
                                        ApprovedBy = defaultSettings.FirstOrDefault().PostUserId,
                                        IsLocked = true,
                                        EntryUserId = defaultSettings.FirstOrDefault().PostUserId,
                                        EntryDateTime = DateTime.Now,
                                        UpdateUserId = defaultSettings.FirstOrDefault().PostUserId,
                                        UpdateDateTime = DateTime.Now,
                                    };

                                    posdb.TrnStockOuts.InsertOnSubmit(newStockOut);
                                    posdb.SubmitChanges();

                                    if (stockOut.ListPOSIntegrationTrnStockOutItem.Any())
                                    {
                                        foreach (var item in stockOut.ListPOSIntegrationTrnStockOutItem.ToList())
                                        {
                                            var currentItem = from d in posdb.MstItems where d.BarCode.Equals(item.ItemCode) && d.MstUnit.Unit.Equals(item.Unit) select d;
                                            if (currentItem.Any())
                                            {
                                                Data.TrnStockOutLine newStockOutLine = new Data.TrnStockOutLine
                                                {
                                                    StockOutId = newStockOut.Id,
                                                    ItemId = currentItem.FirstOrDefault().Id,
                                                    UnitId = currentItem.FirstOrDefault().UnitId,
                                                    Quantity = item.Quantity,
                                                    Cost = item.Cost,
                                                    Amount = item.Amount,
                                                    AssetAccountId = currentItem.FirstOrDefault().AssetAccountId,
                                                };

                                                posdb.TrnStockOutLines.InsertOnSubmit(newStockOutLine);

                                                var updateItem = currentItem.FirstOrDefault();
                                                updateItem.OnhandQuantity = currentItem.FirstOrDefault().OnhandQuantity - Convert.ToDecimal(item.Quantity);

                                                posdb.SubmitChanges();

                                                sysSettingsForm.logMessages(" > " + currentItem.FirstOrDefault().ItemDescription + " * " + item.Quantity.ToString("#,##0.00") + "\r\n\n");
                                            }
                                        }
                                    }

                                    sysSettingsForm.logMessages("Save Successful!" + "\r\n\n");
                                    sysSettingsForm.logMessages("Time Stamp: " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt") + "\r\n\n");
                                    sysSettingsForm.logMessages("\r\n\n");
                                }
                                else
                                {
                                    sysSettingsForm.logMessages("Cannot Save Stock Out: OT - " + stockOut.BranchCode + " - " + stockOut.OTNumber + "\r\n\n");
                                    sysSettingsForm.logMessages("Empty Accounts!" + "\r\n\n");
                                    sysSettingsForm.logMessages("Time Stamp: " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt") + "\r\n\n");
                                    sysSettingsForm.logMessages("\r\n\n");
                                }
                            }
                        }
                    }
                }

                return Task.FromResult("");
            }
            catch (Exception e)
            {
                sysSettingsForm.logMessages("Stock-Out Error: " + e.Message + "\r\n\n");
                sysSettingsForm.logMessages("Time Stamp: " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt") + "\r\n\n");
                sysSettingsForm.logMessages("\r\n\n");

                return Task.FromResult("");
            }
        }
    }
}