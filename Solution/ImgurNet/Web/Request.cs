﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using ImgurNet.Authentication;
using ImgurNet.Converters.Generic;
using ImgurNet.Exceptions;
using ImgurNet.Extensions;
using ImgurNet.Models;
using Newtonsoft.Json;

namespace ImgurNet.Web
{
	internal static class Request
	{
		/// <summary>
		/// 
		/// </summary>
		internal static readonly string ImgurApiV3Base = "https://api.imgur.com/3/{0}";

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="httpMethod"></param>
		/// <param name="endpointUrl"></param>
		/// <param name="authentication"></param>
		/// <param name="queryStrings"></param>
		/// <param name="content"></param>
		/// <returns></returns>
		internal async static Task<ImgurResponse<T>> SubmitRequestAsync<T>(HttpMethod httpMethod, string endpointUrl,
			IAuthentication authentication, Dictionary<string, string> queryStrings = null, HttpContent content = null)
		{
			return await SubmitRequestAsync<T>(httpMethod, new Uri(String.Format(ImgurApiV3Base, endpointUrl)), authentication, queryStrings, content);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="httpMethod"></param>
		/// <param name="endpointUri"></param>
		/// <param name="authentication"></param>
		/// <param name="queryStrings"></param>
		/// <param name="content"></param>
		/// <returns></returns>
		private async static Task<ImgurResponse<T>> SubmitRequestAsync<T>(HttpMethod httpMethod, Uri endpointUri,
			IAuthentication authentication, Dictionary<string, string> queryStrings = null, HttpContent content = null)
		{
			// Set up Query Strings
			if (queryStrings == null)
				queryStrings = new Dictionary<string, string>();

			queryStrings.Add("_", DateTime.UtcNow.ToUnixTimestamp().ToString());
			endpointUri = queryStrings.ToQueryString(endpointUri);

			// Create the Http Client
			var httpClient = new HttpClient();
			switch (authentication.AuthenticationType)
			{
				case AuthenticationType.ClientId:
					var clientAuthentication = authentication as ClientAuthentication;
					if (clientAuthentication == null) 
						throw new InvalidDataException("This should not have happened. The authentication interface is not of type ClientAuthentication, yet it's type says it is. PANIC. (nah, just tweet @alexerax).");

					httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization",
						String.Format("Client-ID {0}", clientAuthentication.ClientId));
					break;

				case AuthenticationType.OAuth:
					throw new NotImplementedException("ImgurNet doesn't support OAuth currently. Soon!");
			}
			
			// Check which request to do, and execute it
			HttpResponseMessage httpResponse;
			switch (httpMethod)
			{
				case HttpMethod.Get:
					httpResponse = await httpClient.GetAsync(endpointUri);
					break;
				case HttpMethod.Post:
					httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
					httpResponse = await httpClient.PostAsync(endpointUri, content ?? new MultipartFormDataContent());
					break;
				case HttpMethod.Delete:
					httpResponse = await httpClient.DeleteAsync(endpointUri);
					break;
				default:
					throw new NotImplementedException("Soon.");
			}

			// Get rate limit figures
			authentication.RateLimit.ClientLimit = int.Parse(httpResponse.Headers.GetValue("X-RateLimit-ClientLimit") ?? "12500");
			authentication.RateLimit.ClientRemaining = int.Parse(httpResponse.Headers.GetValue("X-RateLimit-ClientRemaining") ?? "12500");
			authentication.RateLimit.UserLimit = int.Parse(httpResponse.Headers.GetValue("X-RateLimit-UserLimit") ?? "500");
			authentication.RateLimit.UserRemaining = int.Parse(httpResponse.Headers.GetValue("X-RateLimit-UserRemaining") ?? "500");
			authentication.RateLimit.UserReset = double.Parse(httpResponse.Headers.GetValue("X-RateLimit-UserReset") ?? "0").ToDateTime();

			// Try parsing and validating the output
#if DEBUG
			try
			{
#endif
				var stringResponse = await httpResponse.Content.ReadAsStringAsync();
				var imgurResponse = JsonConvert.DeserializeObject<ImgurResponse<T>>(stringResponse);
				if (imgurResponse.Success)
					return imgurResponse;

				var errorImgurReponse = JsonConvert.DeserializeObject<ImgurResponse<Error>>(stringResponse);
				throw new ImgurResponseFailedException(errorImgurReponse, errorImgurReponse.Data.ErrorDescription);
#if DEBUG
			}
			catch (JsonReaderException ex) { return null; }
#endif
		}
				
		/// <summary>
		/// 
		/// </summary>
		internal enum HttpMethod
		{
			Get,
			Post,
			Put,
			Delete
		}
	}
}
