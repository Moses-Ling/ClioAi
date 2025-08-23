using Microsoft.VisualStudio.TestTools.UnitTesting;
using AudioTranscriptionApp;

namespace AudioTranscriptionApp.Tests
{
    [TestClass]
    public class SettingsTests
    {
        [TestMethod]
        public void EncryptDecrypt_RoundTrip_ShouldReturnOriginal()
        {
            // Arrange
            string originalApiKey = "test-api-key-12345";

            // Act
            string encrypted = EncryptionHelper.EncryptString(originalApiKey);
            Assert.IsNotNull(encrypted, "Encryption returned null.");
            Assert.AreNotEqual(originalApiKey, encrypted, "Encrypted string should not match original.");

            string decrypted = EncryptionHelper.DecryptString(encrypted);

            // Assert
            Assert.AreEqual(originalApiKey, decrypted, "Decrypted string does not match original.");
        }

        [TestMethod]
        public void Encrypt_NullOrEmpty_ShouldReturnEmpty()
        {
            // Act
            string encryptedNull = EncryptionHelper.EncryptString(null);
            string encryptedEmpty = EncryptionHelper.EncryptString(string.Empty);

            // Assert
            Assert.AreEqual(string.Empty, encryptedNull, "Encrypting null should return empty string.");
            Assert.AreEqual(string.Empty, encryptedEmpty, "Encrypting empty string should return empty string.");
        }

        [TestMethod]
        public void Decrypt_NullOrEmpty_ShouldReturnEmpty()
        {
            // Act
            string decryptedNull = EncryptionHelper.DecryptString(null);
            string decryptedEmpty = EncryptionHelper.DecryptString(string.Empty);

            // Assert
            Assert.AreEqual(string.Empty, decryptedNull, "Decrypting null should return empty string.");
            Assert.AreEqual(string.Empty, decryptedEmpty, "Decrypting empty string should return empty string.");
        }
    }
}
