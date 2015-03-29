using Sitecore.ContentSearch;
using Sitecore.ContentSearch.SearchTypes;
using Sitecore.ContentSearch.Security;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Jobs;
using Sitecore.Security.Accounts;
using Sitecore.SecurityModel;
using Sitecore.Shell.Applications.Dialogs.ProgressBoxes;
using Sitecore.Shell.Applications.WebEdit.Commands;
using Sitecore.Shell.Framework.Commands;
using Sitecore.Text;
using Sitecore.Web;
using Sitecore.Web.UI.Sheer;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Sitecore.Data;
using Sitecore.Configuration;

namespace Sitecore.SharedSource.Buckets.SearchOperations
{
    [Serializable]
    public class SetWorkflow : WebEditCommand
    {
        public override void Execute(CommandContext context)
        {
            if (context.Items.Length > 0)
            {
                Item item = context.Items[0];
                NameValueCollection parameters = new NameValueCollection();
                parameters["id"] = item.ID.ToString();
                parameters["language"] = (Context.Language == null) ? item.Language.ToString() : Context.Language.ToString();
                parameters["version"] = item.Version.ToString();
                parameters["database"] = item.Database.Name;
                parameters["user"] = Context.User.Name;
                parameters["isPageEditor"] = (context.Parameters["pageEditor"] == "1") ? "1" : "0";
                parameters["searchString"] = context.Parameters.GetValues("url")[0].Replace("\"", string.Empty);
                Context.ClientPage.Start(this, "Run", parameters);
            }
        }

        protected void Run(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (SheerResponse.CheckModified())
            {
                if (args.IsPostBack)
                {
                    if (args.HasResult)
                    {
                        Item item = Factory.GetDatabase(args.Parameters["database"]).GetItem(args.Parameters["id"]);
                        Item workflowItem = Factory.GetDatabase(args.Parameters["database"]).GetItem(args.Result);
                        if (!workflowItem.TemplateID.Equals(ID.Parse("{1C0ACC50-37BE-4742-B43C-96A07A7410A5}")))
                        {
                            SheerResponse.Alert("Please select a workflow item", new string[0]);
                            return;
                        }
                        string str = args.Parameters["user"];
                        List<SearchStringModel> list = SearchStringModel.ExtractSearchQuery(args.Parameters["searchString"]);
                        if (item != null)
                        {
                            object[] parameters = new object[] { item, list, workflowItem, str };
                            string jobName = "Applying workflow";
                            string title = "Applying workflow on items";
                            string icon = "~/icon/network/16x16/outbox.png";
                            ProgressBox.Execute(jobName, title, icon, new ProgressBoxMethod(this.StartProcess), parameters);
                            SheerResponse.Alert("Workflow '" + workflowItem.Name + "' was applied", new string[0]);
                        }

                    }
                }
                else
                {
                    List<SearchStringModel> searchStringModel = SearchStringModel.ExtractSearchQuery(args.Parameters["searchString"]);
                    SitecoreIndexableItem indexable = Factory.GetDatabase(args.Parameters["database"]).GetItem(args.Parameters["id"]);
                    using (IProviderSearchContext context = ContentSearchManager.GetIndex(indexable).CreateSearchContext(SearchSecurityOptions.EnableSecurityCheck))
                    {
                        if (LinqHelper.CreateQuery<SitecoreUISearchResultItem>(context, searchStringModel, (Item)indexable, null).Any<SitecoreUISearchResultItem>())
                        {
                            UrlString str6 = new UrlString("/sitecore/shell/Applications/Item browser.aspx");
                            str6.Append("ro", "/sitecore/system/Workflows");
                            str6.Append("sc_content", Context.ContentDatabase.Name);
                            str6.Append("filter", "AddTagDialog");
                            ShowItemBrowser("Set workflow on items", "Select a workflow to be applied on all items", "/sitecore/system/Workflows", Context.ContentDatabase.Name);

                            args.WaitForPostBack();
                        }
                    }
                }
            }
        }

        private void StartProcess(params object[] parameters)
        {
            Item item = (Item)parameters[0];
            SitecoreIndexableItem indexable = item;
            if (indexable == null)
            {
                Log.Error("Applying workflow - Unable to cast current item - " + parameters[0].GetType().FullName, this);
            }
            else
            {
                List<SearchStringModel> searchStringModel = (List<SearchStringModel>)parameters[1];
                Item workflowItem = (Item)parameters[2];
                string accountName = (string)parameters[3];
                Job job = Context.Job;
                Assert.IsNotNull(job, "UI Job");
                Language clientLanguage = Context.Language;
                if (job.Options != null)
                {
                    clientLanguage = job.Options.ClientLanguage;
                }
                using (IProviderSearchContext context = ContentSearchManager.GetIndex(indexable).CreateSearchContext(SearchSecurityOptions.EnableSecurityCheck))
                {
                    IQueryable<SitecoreUISearchResultItem> source = LinqHelper.CreateQuery<SitecoreUISearchResultItem>(context, searchStringModel, (Item)indexable, null);
                    if (source.Any<SitecoreUISearchResultItem>())
                    {
                        Account account = Account.FromName(accountName, AccountType.User);
                        int num = source.Count<SitecoreUISearchResultItem>();
                        int num2 = 1;
                        int numActual = 0;
                        string str3 = Translate.TextByLanguage("Processed", clientLanguage);
                        foreach (SitecoreUISearchResultItem item3 in source)
                        {
                            job.Status.Messages.Add(string.Format("{0}: {1}/{2}", str3, num2, num));
                            num2++;
                            Item item4 = item3.GetItem();
                            if (item4 != null)
                            {
                                using (new SecurityEnabler())
                                {
                                    if (!item4.Security.CanWrite(account))
                                    {
                                        continue;
                                    }
                                    //If item already in workflow, skip it
                                    if (!String.IsNullOrWhiteSpace(item4[Sitecore.FieldIDs.Workflow]))
                                        continue;

                                    Sitecore.Workflows.IWorkflow workflow = item.Database.WorkflowProvider.GetWorkflow(workflowItem.ID.ToString());

                                    workflow.Start(item4);
                                    numActual++;
                                }

                            }
                        }
                    }
                }
            }
        }

        public static void ShowItemBrowser(string header, string text, string root, string database)
        {
            UrlString str2 = new UrlString("/sitecore/shell/Applications/Item browser.aspx");
            str2.Append("sc_content", database);

            str2.Append("ro", root);
            str2.Append("he", header);
            str2.Append("txt", text);

            SheerResponse.ShowModalDialog(str2.ToString(), true);

        }
    }
}
