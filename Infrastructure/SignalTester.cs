using System;
using System.Net.Http;
using System.Threading.Tasks;
using Core.Models;

namespace Infrastructure
{
    public class SignalTester
    {
        private readonly HttpClient _client;

        public SignalTester(HttpClient client)
        {
            _client = client;
        }

        public async Task<bool> IsLinkAlive(string link)
        {
            try
            {
                using (HttpResponseMessage response = await _client.GetAsync(new Uri(link)))
                {
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}