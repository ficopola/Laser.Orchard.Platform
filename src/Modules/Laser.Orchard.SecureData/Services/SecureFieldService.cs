﻿using Laser.Orchard.SecureData.Fields;
using Laser.Orchard.SecureData.Security;
using Orchard.ContentManagement;
using Orchard.Environment.Configuration;
using Orchard.Localization;
using Orchard.Security;
using Orchard.Security.Permissions;
using Orchard.Utility.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace Laser.Orchard.SecureData.Services {
    public class SecureFieldService : ISecureFieldService {
        private readonly IEncryptionService _encryptionService;
        private readonly ShellSettings _shellSettings;

        public Localizer T { get; set; }

        public SecureFieldService(IEncryptionService encryptionService, ShellSettings shellSettings) {
            _encryptionService = encryptionService;
            _shellSettings = shellSettings;

            T = NullLocalizer.Instance;
        }

        private string DecodeString(string str, string encryptionKey) {
            return Encoding.UTF8.GetString(Decode(Convert.FromBase64String(str), Encoding.UTF8.GetBytes(encryptionKey)));
        }

        public string EncodeString(string str, string encryptionKey) {
            //return Convert.ToBase64String(_encryptionService.Encode(Encoding.UTF8.GetBytes(str)));
            return Convert.ToBase64String(Encode(Encoding.UTF8.GetBytes(str), Encoding.UTF8.GetBytes(encryptionKey)));
        }

        public string DecodeValue(EncryptedStringField field) {
            if (field == null || field.Value == null) {
                return null;
            }
            return DecodeString(field.Value, field.DisplayName);
        }

        public void EncodeValue(EncryptedStringField field, string value) {
            // Encoding.UTF8.GetBytes can't encode null values.
            if (value != null) {
                field.Value = EncodeString(value, field.DisplayName);
            } else {
                field.Value = null;
            }
        }

        public bool IsValueEqual(EncryptedStringField field, string value) {
            return string.Equals(field.Value, EncodeString(value, field.DisplayName), StringComparison.Ordinal);
        }

        public Permission GetOwnPermission(string partName, string fieldName) {
            string fieldFullName = partName + "." + fieldName;
            return new Permission {
                Description = T("Manage own {0} encrypted string fields", fieldFullName).Text,
                Name = "ManageOwnEncryptedStringFields_" + fieldFullName,
                ImpliedBy = new Permission[] {
                    EncryptedStringFieldPermissions.ManageOwnEncryptedStringFields,
                    GetAllPermission(partName, fieldName)
                }
            };
        }

        public Permission GetOwnPermission(ContentPart part, EncryptedStringField field) {
            return GetOwnPermission(part.PartDefinition.Name, field.Name);
        }

        public Permission GetAllPermission(string partName, string fieldName) {
            string fieldFullName = partName + "." + fieldName;
            return new Permission {
                Description = T("Manage all {0} encrypted string fields", fieldFullName).Text,
                Name = "ManagAllEncryptedStringFields_" + fieldFullName,
                ImpliedBy = new Permission[] {
                    EncryptedStringFieldPermissions.ManageAllEncryptedStringFields
                }
            };
        }

        public Permission GetAllPermission(ContentPart part, EncryptedStringField field) {
            return GetAllPermission(part.PartDefinition.Name, field.Name);
        }

        #region "Encode Algorithm"
        public byte[] Encode(byte[] data, byte[] encryptionKey) {
            // cipherText ::= IV || ENC(EncryptionKey, IV, plainText) || HMAC(SigningKey, IV || ENC(EncryptionKey, IV, plainText))

            byte[] encryptedData;
            // TODO: Verificare eccezione generata da ToByteArray().
            byte[] iv = encryptionKey;

            using (var ms = new MemoryStream()) {
                using (var symmetricAlgorithm = CreateSymmetricAlgorithm(iv)) {
                    //iv = encryptionKey.ToByteArray();

                    using (var cs = new CryptoStream(ms, symmetricAlgorithm.CreateEncryptor(), CryptoStreamMode.Write)) {
                        cs.Write(data, 0, data.Length);
                        cs.FlushFinalBlock();
                    }

                    encryptedData = ms.ToArray();
                }
            }

            byte[] signedData;

            // signing IV || encrypted data
            using (var hashAlgorithm = CreateHashAlgorithm()) {
                signedData = hashAlgorithm.ComputeHash(iv.Concat(encryptedData).ToArray());
            }

            return iv.Concat(encryptedData).Concat(signedData).ToArray();
        }

        public byte[] Decode(byte[] encodedData, byte[] encryptionKey) {
            // extract parts of the encoded data
            using (var symmetricAlgorithm = CreateSymmetricAlgorithm(encryptionKey)) {
                using (var hashAlgorithm = CreateHashAlgorithm()) {
                    // TODO: Verificare eccezione generata da ToByteArray().
                    var iv = new byte[encryptionKey.Length];
                    var signature = new byte[hashAlgorithm.HashSize / 8];
                    var data = new byte[encodedData.Length - iv.Length - signature.Length];

                    Array.Copy(encodedData, 0, iv, 0, iv.Length);
                    Array.Copy(encodedData, iv.Length, data, 0, data.Length);
                    Array.Copy(encodedData, iv.Length + data.Length, signature, 0, signature.Length);

                    // validate the signature
                    var mac = hashAlgorithm.ComputeHash(iv.Concat(data).ToArray());

                    if (!mac.SequenceEqual(signature)) {
                        // message has been tampered
                        throw new ArgumentException();
                    }

                    symmetricAlgorithm.IV = iv;

                    using (var ms = new MemoryStream()) {
                        using (var cs = new CryptoStream(ms, symmetricAlgorithm.CreateDecryptor(), CryptoStreamMode.Write)) {
                            cs.Write(data, 0, data.Length);
                            cs.FlushFinalBlock();
                        }
                        return ms.ToArray();
                    }
                }
            }
        }

        private SymmetricAlgorithm CreateSymmetricAlgorithm(byte[] encryptionKey) {
            var algorithm = SymmetricAlgorithm.Create(_shellSettings.EncryptionAlgorithm);
            algorithm.Key = encryptionKey;
            return algorithm;
        }

        private HMAC CreateHashAlgorithm() {
            var algorithm = HMAC.Create(_shellSettings.HashAlgorithm);
            algorithm.Key = _shellSettings.HashKey.ToByteArray();
            return algorithm;
        }
        #endregion

    }
}