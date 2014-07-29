﻿// /*
//  Copyright (c) Stefan Gordon
//  All Rights Reserved
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the
//  License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// 
//  THIS CODE IS PROVIDED ON AN *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, EITHER EXPRESS OR IMPLIED,
//  INCLUDING WITHOUT LIMITATION ANY IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
//  MERCHANTABLITY OR NON-INFRINGEMENT.
// 
//  See the Apache 2 License for the specific language governing permissions and limitations under the License.
//  */

#region

using System;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;

#endregion

namespace AzureEncryptionExtensions.Providers
{
    public sealed class SymmetricBlobCryptoProvider : IBlobCryptoProvider
    {
        public byte[] Key { get; private set; }

        public SymmetricBlobCryptoProvider()
        {
            using (AesCryptoServiceProvider aes = new AesCryptoServiceProvider())
                Key = aes.Key;
        }

        public SymmetricBlobCryptoProvider(byte[] key)
        {
            InitializeFromKeyBytes(key);
        }

        public void InitializeFromKeyBytes(byte[] key)
        {
            Key = key;
        }

        public void WriteKeyFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("path", "Must provide valid file path.");

            File.WriteAllText(path, ToKeyFileString());
        }

        public string ToKeyFileString()
        {
            KeyFileStorage keyStorage = new KeyFileStorage
            {
                KeyMaterial = Key,
                ProviderType = GetType().ToString(),
                ContainsPrivateKey = true
            };

            return JsonConvert.SerializeObject(keyStorage);
        }


        public Stream EncryptedStream(Stream streamToEncrypt)
        {
            using (AesCryptoServiceProvider aesAlg = new AesCryptoServiceProvider())
            {
                // Set key but retain randomized IV created during provider instantiation.
                aesAlg.Key = Key;

                // Create an encryptor to perform the stream transform.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor();

                MemoryStream ivStream = new MemoryStream(aesAlg.IV);
                CryptoStream cryptoStream = new CryptoStream(streamToEncrypt, encryptor, CryptoStreamMode.Read);

                return new ConcatenatedStream(ivStream, cryptoStream);
            }
        }

        public Stream DecryptedStream(Stream streamToDecrypt)
        {
            using (AesCryptoServiceProvider aesAlg = new AesCryptoServiceProvider())
            {
                // Attempt to read IV from Stream
                byte[] ivBytes= new byte[aesAlg.BlockSize / 8];
                streamToDecrypt.Read(ivBytes, 0, ivBytes.Length);

                // Set key and initialization vector
                aesAlg.Key = Key;
                aesAlg.IV = ivBytes;

                // Create a decryptor to perform the stream transform.
                ICryptoTransform decryptor = aesAlg.CreateDecryptor();

                CryptoStream cryptoStream = new CryptoStream(streamToDecrypt, decryptor, CryptoStreamMode.Read);

                return cryptoStream;
            }
        }
    }
}
