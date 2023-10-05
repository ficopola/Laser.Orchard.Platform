﻿using DotNetOpenAuth.AspNet.Clients;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Orchard.Environment.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;

namespace Laser.Orchard.OpenAuthentication.Services {
    [OrchardFeature("Laser.Orchard.OpenAuthentication.Keycloak")]
    public class OpenAuthKeycloakOauth2Client : OAuth2Client {
        #region endpoints
        // These strings are used in string.Format() calls to have the actual endpoints
        // {0}: the "base url" of the keycloak IDP
        // {1}: the keycloak realm. This is Case sensitive

        // The well-known configuration endpoint returns an object that describes
        // the other endpoints.
        // TODO: use this endpoint to fetch the full configuration, rather than 
        // hardcoding only the portions we need.
        private const string WellKnownEndpointFormat = 
            "{0}/realms/{1}/.well-known/openid-configuration";
        private const string AuthorizationEndpointFormat =
            "{0}/realms/{1}/protocol/openid-connect/auth";
        private const string TokenEndpointFormat =
            "{0}/realms/{1}/protocol/openid-connect/token";
        private const string UserInfoEndpointFormat =
            "{0}/realms/{1}/protocol/openid-connect/userinfo";
        #endregion


        private readonly string _clientId;

        private readonly string _idpUrl;
        private readonly string _realm;
        private readonly string[] _requestedScopes;

        public OpenAuthKeycloakOauth2Client(
            string clientId, string idpUrl, string realm, params string[] requestedScopes)
            : base("Keycloak") {

            _clientId = clientId;
            _idpUrl = idpUrl;
            _realm = realm;
            _requestedScopes = requestedScopes ?? new string[] { };
        }

        public OpenAuthKeycloakOauth2Client(string clientId, string idpUrl, string realm) 
            : this(clientId, idpUrl, realm, new[] { "openid", "email" }) { }

        protected override Uri GetServiceLoginUrl(Uri returnUrl) {
            var state = string.IsNullOrEmpty(returnUrl.Query) ? string.Empty : returnUrl.Query.Substring(1);

            return OAuthHelpers.BuildUri(string.Format(AuthorizationEndpointFormat, _idpUrl, _realm), 
                new NameValueCollection
                {
                    { "client_id", _clientId },
                    { "response_type", "code" },
                    { "scope", string.Join(" ", _requestedScopes) },
                    { "redirect_uri", returnUrl.GetLeftPart(UriPartial.Path) },
                    { "state", state },
                });
        }

        public IDictionary<string, string> GetUserDataDictionary(string accessToken) {
            return GetUserData(accessToken);
        }

        protected override IDictionary<string, string> GetUserData(string accessToken) {
            var uri = OAuthHelpers.BuildUri(string.Format(UserInfoEndpointFormat, _idpUrl, _realm), 
                new NameValueCollection { { "access_token", accessToken } });
            var webRequest = (HttpWebRequest)WebRequest.Create(uri);

            using (var webResponse = webRequest.GetResponse()) {
                using (var stream = webResponse.GetResponseStream()) {
                    if (stream != null) {
                        using (var textReader = new StreamReader(stream)) {
                            var json = textReader.ReadToEnd();
                            var extraData = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                            return extraData;
                        }
                    }
                }
            }
            return null;
        }

        public string GetAccessToken(Uri returnUrl, string authorizationCode) {
            return QueryAccessToken(returnUrl, authorizationCode);
        }

        protected override string QueryAccessToken(Uri returnUrl, string authorizationCode) {

            var postData = HttpUtility.ParseQueryString(string.Empty);
            postData.Add(new NameValueCollection
                {
                    { "grant_type", "authorization_code" },
                    { "code", authorizationCode },
                    { "redirect_uri", returnUrl.GetLeftPart(UriPartial.Path) },
                });

            var webRequest = (HttpWebRequest)WebRequest.Create(
                string.Format(TokenEndpointFormat, _idpUrl, _realm));
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            using (var s = webRequest.GetRequestStream()) {
                using (var sw = new StreamWriter(s)) {
                    sw.Write(postData.ToString());
                }
            }

            using (var webResponse = webRequest.GetResponse()) {
                using (var stream = webResponse.GetResponseStream()) {
                    if (stream != null) {
                        using (var reader = new StreamReader(stream)) {
                            var response = reader.ReadToEnd();
                            var json = JObject.Parse(response);
                            var accessToken = json.Value<string>("access_token");
                            return accessToken;
                        }
                    }
                }
            }
            return null;
        }
    }
}