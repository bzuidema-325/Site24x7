using System;
using System.Collections.Generic;

namespace Site24x7
{
    /// <summary>
    /// Site24x7 data centers. Each one has its own API root and its own Zoho Accounts server,
    /// and a token issued by one data center is not accepted by another.
    /// </summary>
    public enum Site24x7DataCenter
    {
        US,
        EU,
        CN,
        IN,
        AU,
        JP,
        CA,
        UK,
        AE,
        SA
    }

    /// <summary>
    /// API and accounts endpoints for every data center, taken from the Site24x7 API documentation
    /// (https://www.site24x7.com/help/api/#introduction and #authentication).
    /// </summary>
    internal static class DataCenterEndpoints
    {
        private static readonly Dictionary<Site24x7DataCenter, (string Api, string Accounts)> Map =
            new Dictionary<Site24x7DataCenter, (string, string)>
            {
                { Site24x7DataCenter.US, ("https://www.site24x7.com/api/", "https://accounts.zoho.com/") },
                { Site24x7DataCenter.EU, ("https://www.site24x7.eu/api/", "https://accounts.zoho.eu/") },
                { Site24x7DataCenter.CN, ("https://www.site24x7.cn/api/", "https://accounts.zoho.com.cn/") },
                { Site24x7DataCenter.IN, ("https://www.site24x7.in/api/", "https://accounts.zoho.in/") },
                { Site24x7DataCenter.AU, ("https://www.site24x7.net.au/api/", "https://accounts.zoho.com.au/") },
                { Site24x7DataCenter.JP, ("https://app.site24x7.jp/api/", "https://accounts.zoho.jp/") },
                { Site24x7DataCenter.CA, ("https://www.site24x7.ca/api/", "https://accounts.zohocloud.ca/") },
                { Site24x7DataCenter.UK, ("https://app.site24x7.uk/api/", "https://accounts.zoho.uk/") },
                { Site24x7DataCenter.AE, ("https://app.site24x7.ae/api/", "https://accounts.zoho.ae/") },
                { Site24x7DataCenter.SA, ("https://www.site24x7.sa/api/", "https://accounts.zoho.sa/") },
            };

        public static Uri ApiBase(Site24x7DataCenter dc) => new Uri(Map[dc].Api);

        public static Uri AccountsBase(Site24x7DataCenter dc) => new Uri(Map[dc].Accounts);
    }
}
