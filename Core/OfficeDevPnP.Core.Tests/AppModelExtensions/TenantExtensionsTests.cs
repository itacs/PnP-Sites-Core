﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SharePoint.Client;
using Microsoft.Online.SharePoint.TenantAdministration;
using System.Configuration;
using OfficeDevPnP.Core.Entities;
using System.Threading;
using OfficeDevPnP.Core.Utilities;

namespace OfficeDevPnP.Core.Tests.AppModelExtensions
{
#if !CLIENTSDKV15
    [TestClass()]
    public class TenantExtensionsTests
    {
        private string sitecollectionName = "TestPnPSC_123456789_";

        #region Test initialize and cleanup
        [ClassInitialize()]
        public static void ClassInit(TestContext context)
        {
            using (var tenantContext = TestCommon.CreateTenantClientContext())
            {
                CleanupAllTestSiteCollections(tenantContext);
            }
        }

        [ClassCleanup()]
        public static void ClassCleanup()
        {
            using (var tenantContext = TestCommon.CreateTenantClientContext())
            {
                CleanupAllTestSiteCollections(tenantContext);
            }
        }

        private static void CleanupAllTestSiteCollections(ClientContext tenantContext)
        {
            var tenant = new Tenant(tenantContext);
            for (int i = 0; i <= 9; i++)
            {
                try
                {
                    string site = string.Format("TestPnPSC_123456789_{0}", i);
                    site = GetTestSiteCollectionName(ConfigurationManager.AppSettings["SPODevSiteUrl"], site);
                    // ensure the site collection in unlocked state before deleting
                    tenant.SetSiteLockState(site, SiteLockState.Unlock);
                    // delete the site collection, do not use the recyle bin
                    tenant.DeleteSiteCollection(site, false);
                }
                catch (Exception ex)
                {
                    // eat all exceptions
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        [TestInitialize()]
        public void Initialize()
        {
            sitecollectionName = sitecollectionName + (new Random().Next(0, 9)).ToString();
            using (var tenantContext = TestCommon.CreateTenantClientContext())
            {
                //Ensure nothing was left behind before we run our tests
                CleanupCreatedTestSiteCollections(tenantContext);
            }
        }
        #endregion

        #region Get site collections tests
        [TestMethod()]
        public void GetSiteCollectionsTest()
        {
            using (var tenantContext = TestCommon.CreateTenantClientContext())
            {
                var tenant = new Tenant(tenantContext);
                var siteCols = tenant.GetSiteCollections();

                Assert.IsTrue(siteCols.Any());

            }
        }

        [TestMethod()]
        public void GetOneDriveSiteCollectionsTest()
        {
            if (TestCommon.AppOnlyTesting())
            {
                Assert.Inconclusive("Web service tests are not supported when testing using app-only");
            }

            using (var tenantContext = TestCommon.CreateTenantClientContext())
            {
                var tenant = new Tenant(tenantContext);
                var siteCols = tenant.GetOneDriveSiteCollections();

                Assert.IsTrue(siteCols.Any());

            }
        }

        [TestMethod()]
        public void GetUserProfileServiceClientTest() {
            if (TestCommon.AppOnlyTesting())
            {
                Assert.Inconclusive("Web service tests are not supported when testing using app-only");
            }

            using (var tenantContext = TestCommon.CreateTenantClientContext())
            {
                var tenant = new Tenant(tenantContext);
                var serviceClient = tenant.GetUserProfileServiceClient();
                tenantContext.Load(tenantContext.Web, w => w.CurrentUser);
                tenantContext.ExecuteQueryRetry();

                var profile = serviceClient.GetUserProfileByName(tenantContext.Web.CurrentUser.LoginName);

                Assert.IsNotNull(profile);
            }
        }
        #endregion

        #region Site existance tests
        [TestMethod()]
        public void CheckIfSiteExistsTest() {
            using (var tenantContext = TestCommon.CreateTenantClientContext()) {
                var tenant = new Tenant(tenantContext);
                var siteCollections = tenant.GetSiteCollections();

                var site = siteCollections.First();
                var siteExists1 = tenant.CheckIfSiteExists(site.Url, "Active");
                Assert.IsTrue(siteExists1);

                try {
                    var siteExists2 = tenant.CheckIfSiteExists(site.Url + "sites/aaabbbccc", "Active");
                    Assert.IsFalse(siteExists2, "Invalid site returned as valid.");
                }
                catch (ServerException) { }
            }
        }

        [TestMethod()]
        public void SiteExistsTest()
        {
            using (var tenantContext = TestCommon.CreateTenantClientContext())
            {
                var tenant = new Tenant(tenantContext);
                var siteCollections = tenant.GetSiteCollections();

                var site = siteCollections.First();
                var siteExists1 = tenant.SiteExists(site.Url);
                Assert.IsTrue(siteExists1);

                var siteExists2 = tenant.SiteExists(site.Url + "sites/aaabbbccc");
                Assert.IsFalse(siteExists2, "Invalid site returned as valid.");
            }
        }

        [TestMethod]
        public void SubSiteExistsTest()
        {
            using (var tenantContext = TestCommon.CreateTenantClientContext())
            {
                var tenant = new Tenant(tenantContext);
                string devSiteUrl = ConfigurationManager.AppSettings["SPODevSiteUrl"];
                string siteToCreateUrl = CreateTestSiteCollection(tenant, sitecollectionName);
                string subSiteUrlGood = "";
                string subSiteUrlWrong = "";

                Site site = tenant.GetSiteByUrl(siteToCreateUrl);
                tenant.Context.Load(site);
                tenant.Context.ExecuteQueryRetry();
                Web web = site.RootWeb;
                web.Context.Load(web);
                web.Context.ExecuteQueryRetry();

                //Create sub site
                SiteEntity sub = new SiteEntity() { Title = "Test Sub", Url = "sub", Description = "Test" };
                web.CreateWeb(sub);
                siteToCreateUrl = UrlUtility.EnsureTrailingSlash(siteToCreateUrl);
                subSiteUrlGood = String.Format("{0}{1}", siteToCreateUrl, sub.Url);
                subSiteUrlWrong = String.Format("{0}{1}", siteToCreateUrl, "8988980");

                // Check real sub site
                bool subSiteExists = tenant.SubSiteExists(subSiteUrlGood);
                Assert.IsTrue(subSiteExists);

                // check non existing sub site
                bool subSiteExists2 = tenant.SubSiteExists(subSiteUrlWrong);
                Assert.IsFalse(subSiteExists2);

                // check root site (= site collection). Will return true when existing
                bool subSiteExists3 = tenant.SubSiteExists(siteToCreateUrl);
                Assert.IsTrue(subSiteExists3);

                // check root site (= site collection) that does not exist. Will return false when non-existant
                bool subSiteExists4 = tenant.SubSiteExists(siteToCreateUrl + "8808809808");
                Assert.IsFalse(subSiteExists4);
            }
        }
        #endregion

        #region Site collection creation and deletion tests
        [TestMethod]
        public void CreateDeleteSiteCollectionTest()
        {
            using (var tenantContext = TestCommon.CreateTenantClientContext())
            {
                var tenant = new Tenant(tenantContext);

                //Create site collection test
                string siteToCreateUrl = CreateTestSiteCollection(tenant, sitecollectionName);
                var siteExists = tenant.SiteExists(siteToCreateUrl);
                Assert.IsTrue(siteExists, "Site collection creation failed");

                //Delete site collection test: move to recycle bin
                tenant.DeleteSiteCollection(siteToCreateUrl, true);
                bool recycled = tenant.CheckIfSiteExists(siteToCreateUrl, "Recycled");
                Assert.IsTrue(recycled, "Site collection recycling failed");

                //Remove from recycle bin
                tenant.DeleteSiteCollectionFromRecycleBin(siteToCreateUrl, true);
                var siteExists2 = tenant.SiteExists(siteToCreateUrl);
                Assert.IsFalse(siteExists2, "Site collection deletion from recycle bin failed");
            }
        }
        #endregion

        #region Site lockstate tests
        [TestMethod]
        public void SetSiteLockStateTest()
        {
            using (var tenantContext = TestCommon.CreateTenantClientContext())
            {
                tenantContext.RequestTimeout = Timeout.Infinite;

                var tenant = new Tenant(tenantContext);
                string devSiteUrl = ConfigurationManager.AppSettings["SPODevSiteUrl"];
                string siteToCreateUrl = GetTestSiteCollectionName(devSiteUrl, sitecollectionName);

                if (!tenant.SiteExists(siteToCreateUrl))
                {
                    siteToCreateUrl = CreateTestSiteCollection(tenant, sitecollectionName);
                    var siteExists = tenant.SiteExists(siteToCreateUrl);
                    Assert.IsTrue(siteExists, "Site collection creation failed");
                }

                // Set Lockstate NoAccess test
                tenant.SetSiteLockState(siteToCreateUrl, SiteLockState.NoAccess, true);
                var siteProperties = tenant.GetSitePropertiesByUrl(siteToCreateUrl, true);
                tenantContext.Load(siteProperties);
                tenantContext.ExecuteQueryRetry();
                Assert.IsTrue(siteProperties.LockState == SiteLockState.NoAccess.ToString(), "LockState wasn't set to NoAccess");

                // Set Lockstate NoAccess test
                tenant.SetSiteLockState(siteToCreateUrl, SiteLockState.Unlock, true);
                var siteProperties2 = tenant.GetSitePropertiesByUrl(siteToCreateUrl, true);
                tenantContext.Load(siteProperties2);
                tenantContext.ExecuteQueryRetry();
                Assert.IsTrue(siteProperties2.LockState == SiteLockState.Unlock.ToString(), "LockState wasn't set to UnLock");

                //Delete site collection, also
                tenant.DeleteSiteCollection(siteToCreateUrl, false);
                var siteExists2 = tenant.SiteExists(siteToCreateUrl);
                Assert.IsFalse(siteExists2, "Site collection deletion, including from recycle bin, failed");
            }
        }
        #endregion

        #region Private helper methods
        private static string GetTestSiteCollectionName(string devSiteUrl, string siteCollection)
        {
            Uri u = new Uri(devSiteUrl);
            string host = String.Format("{0}://{1}:{2}", u.Scheme, u.DnsSafeHost, u.Port);

            string path = u.AbsolutePath;
            if (path.EndsWith("/"))
            {
                path = path.Substring(0, path.Length - 1);
            }
            path = path.Substring(0, path.LastIndexOf('/'));

            return string.Format("{0}{1}/{2}", host, path, siteCollection);
        }

        private void CleanupCreatedTestSiteCollections(ClientContext tenantContext)
        {
            string devSiteUrl = ConfigurationManager.AppSettings["SPODevSiteUrl"];
            String testSiteCollection = GetTestSiteCollectionName(devSiteUrl, sitecollectionName);

            //Ensure the test site collection was deleted and removed from recyclebin
            var tenant = new Tenant(tenantContext);
            if (tenant.SiteExists(testSiteCollection))
            {
                tenant.DeleteSiteCollection(testSiteCollection, false);
            }
        }

        private string CreateTestSiteCollection(Tenant tenant, string sitecollectionName)
        {
            string devSiteUrl = ConfigurationManager.AppSettings["SPODevSiteUrl"];
            string siteToCreateUrl = GetTestSiteCollectionName(devSiteUrl, sitecollectionName);

            string siteOwnerLogin = ConfigurationManager.AppSettings["SPOUserName"];
            if (TestCommon.AppOnlyTesting())
            {
                using (var clientContext = TestCommon.CreateClientContext())
                {
                    List<UserEntity> admins = clientContext.Web.GetAdministrators();
                    siteOwnerLogin = admins[0].LoginName.Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries)[2];
                }
            }

            SiteEntity siteToCreate = new SiteEntity()
            {
                Url = siteToCreateUrl,
                Template = "STS#0",
                Title = "Test",
                Description = "Test site collection",
                SiteOwnerLogin = siteOwnerLogin,
            };

            tenant.CreateSiteCollection(siteToCreate, false, true);
            return siteToCreateUrl;
        }
        #endregion
    }
#endif
}
