using QobuzApiSharp.Service;
using System;

namespace QobuzDownloaderX.Shared
{
    public static class QobuzApiServiceManager
    {
        private static QobuzApiService apiService;

        public static QobuzApiService GetApiService()
        {
            if (apiService == null)
            {
                throw new InvalidOperationException("QobuzApiService not initialized");
            }

            return apiService;
        }

        public static void Initialize(string appId, string appSecret)
        {
            apiService?.Dispose();
            apiService = new QobuzApiService(appId, appSecret);
        }

        public static void Initialize()
        {
            apiService?.Dispose();
            apiService = new QobuzApiService();
        }

        public static void ReleaseApiService()
        {
            if (apiService != null)
            {
                using (apiService)
                {
                    apiService = null;
                }
            }
        }
    }
}