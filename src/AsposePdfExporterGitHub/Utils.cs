using Octokit;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Octokit.Internal;

namespace Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter
{
    public static class Extensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>
            (this IDictionary<TKey, TValue> dictionary, TKey key) => dictionary.TryGetValue(key, out var value) ? value : default;
    }

    public static  class Utils
    {
        public static ApiOptions ApiOptions(int? pageSize = null,
            int? startPage = null)
        {
            return new ApiOptions
            {
                PageSize = pageSize,
                PageCount = pageSize.HasValue || startPage.HasValue ? (int?)1 : null,
                StartPage = startPage
            };
        }

        public static Model.ResultPage ToResult(IEnumerable<dynamic> list, int? pageNo)
        {
            return new Model.ResultPage
            {
                Result = null != list ? list.ToList() : new List<dynamic>(),
                PageNo = pageNo
            };
        }
    }
}
