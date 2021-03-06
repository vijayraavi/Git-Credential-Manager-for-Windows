﻿/**** Git Credential Manager for Windows ****
 *
 * Copyright (c) Microsoft Corporation
 * All rights reserved.
 *
 * MIT License
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the """"Software""""), to deal
 * in the Software without restriction, including without limitation the rights to
 * use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
 * the Software, and to permit persons to whom the Software is furnished to do so,
 * subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
 * FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
 * COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN
 * AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
 * WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE."
**/

using System;
using System.Threading.Tasks;

namespace Microsoft.Alm.Authentication
{
    public sealed class SecretStore : BaseSecureStore, ICredentialStore, ITokenStore
    {
        /// <summary>
        /// Creates a new <see cref="SecretStore"/> backed by the operating system keychain / secrets vault.
        /// </summary>
        /// <param name="namespace">The namespace of the secrets written and read by this store.</param>
        /// <param name="credentialCache">Write-through, read-first cache. Default cache is used if a custom cache is not provided.</param>
        /// <param name="tokenCache">Write-through, read-first cache. Default cache is used if a custom cache is not provided.</param>
        /// <param name="getTargetName">Delegate used to transform a `<see cref="TargetUri"/>` into a store lookup key.</param>
        public SecretStore(
            RuntimeContext context,
            string @namespace,
            ICredentialStore credentialCache,
            ITokenStore tokenCache,
            Secret.UriNameConversionDelegate getTargetName)
            : base(context)
        {
            if (@namespace is null)
                throw new ArgumentNullException(nameof(@namespace));
            if (@namespace.IndexOfAny(IllegalCharacters) != -1)
            {
                var inner = new FormatException("Namespace contains illegal characters.");
                throw new ArgumentException(inner.Message, nameof(@namespace), inner);
            }

            _getTargetName = getTargetName ?? Secret.UriToName;

            _namespace = @namespace;
            _credentialCache = credentialCache ?? new SecretCache(context, @namespace, _getTargetName);
            _tokenCache = tokenCache ?? new SecretCache(context, @namespace, _getTargetName);
        }

        public SecretStore(RuntimeContext context, string @namespace, Secret.UriNameConversionDelegate getTargetName)
            : this(context, @namespace, null, null, getTargetName)
        { }

        public SecretStore(RuntimeContext context, string @namespace)
            : this(context, @namespace, null, null, null)
        { }

        private readonly string _namespace;
        private ICredentialStore _credentialCache;
        private Secret.UriNameConversionDelegate _getTargetName;
        private ITokenStore _tokenCache;

        public string Namespace
        {
            get { return _namespace; }
        }

        public Secret.UriNameConversionDelegate UriNameConversion
        {
            get { return _getTargetName; }
            set
            {
                if (value is null)
                    throw new ArgumentNullException(nameof(UriNameConversion));

                _getTargetName = value;
            }
        }

        public async Task<bool> DeleteCredentials(TargetUri targetUri)
        {
            if (targetUri is null)
                throw new ArgumentNullException(nameof(targetUri));

            string targetName = GetTargetName(targetUri);

            return Delete(targetName)
                && await _credentialCache.DeleteCredentials(targetUri);
        }

        public async Task<bool> DeleteToken(TargetUri targetUri)
        {
            if (targetUri is null)
                throw new ArgumentNullException(nameof(targetUri));

            string targetName = GetTargetName(targetUri);

            return Delete(targetName)
                && await _tokenCache.DeleteToken(targetUri);
        }

        public Task PurgeCredentials()
        {
            PurgeCredentials(_namespace);

            return Task.FromResult(true);
        }

        public async Task<Credential> ReadCredentials(TargetUri targetUri)
        {
            if (targetUri is null)
                throw new ArgumentNullException(nameof(targetUri));

            string targetName = GetTargetName(targetUri);

            return await _credentialCache.ReadCredentials(targetUri)
                ?? ReadCredentials(targetName);
        }

        public async Task<Token> ReadToken(TargetUri targetUri)
        {
            if (targetUri is null)
                throw new ArgumentNullException(nameof(targetUri));

            string targetName = GetTargetName(targetUri);

            return await _tokenCache.ReadToken(targetUri)
                ?? ReadToken(targetName);
        }

        public async Task<bool> WriteCredentials(TargetUri targetUri, Credential credentials)
        {
            if (targetUri is null)
                throw new ArgumentNullException(nameof(targetUri));
            if (credentials is null)
                throw new ArgumentNullException(nameof(credentials));

            string targetName = GetTargetName(targetUri);

            return WriteCredential(targetName, credentials)
                && await _credentialCache.WriteCredentials(targetUri, credentials);
        }

        public async Task<bool> WriteToken(TargetUri targetUri, Token token)
        {
            if (targetUri is null)
                throw new ArgumentNullException(nameof(targetUri));
            if (token is null)
                throw new ArgumentNullException(nameof(token));

            string targetName = GetTargetName(targetUri);

            return WriteToken(targetName, token)
                && await _tokenCache.WriteToken(targetUri, token);
        }

        protected override string GetTargetName(TargetUri targetUri)
        {
            if (targetUri is null)
                throw new ArgumentNullException(nameof(targetUri));

            return _getTargetName(targetUri, _namespace);
        }
    }
}
