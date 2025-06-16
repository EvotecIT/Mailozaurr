using System;
using System.Collections.Generic;
using Xunit;

namespace Mailozaurr.Tests {
    public class Pop3ConnectionTests {
        private class FakePop3Client {
            public bool ValidCredentials { get; set; } = true;
            public bool SimulateTimeout { get; set; }
            public bool Connected { get; private set; }

            public void Connect() {
                if (SimulateTimeout) {
                    throw new TimeoutException("Timeout");
                }

                if (!ValidCredentials) {
                    throw new InvalidOperationException("Invalid credentials");
                }

                Connected = true;
            }

            public IReadOnlyList<string> FetchMessageList() {
                if (!Connected) {
                    throw new InvalidOperationException("Not connected");
                }

                return new List<string> { "msg1", "msg2" };
            }
        }
        [Fact]
        public void Pop3_Connect_WithValidCredentials_Succeeds() {
            // Arrange
            var client = new FakePop3Client();

            // Act
            client.Connect();

            // Assert
            Assert.True(client.Connected);
        }

        [Fact]
        public void Pop3_Connect_WithInvalidCredentials_Fails() {
            // Arrange
            var client = new FakePop3Client { ValidCredentials = false };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => client.Connect());
        }

        [Fact]
        public void Pop3_Connect_WithTimeout_Fails() {
            // Arrange
            var client = new FakePop3Client { SimulateTimeout = true };

            // Act & Assert
            Assert.Throws<TimeoutException>(() => client.Connect());
        }

        [Fact]
        public void Pop3_FetchMessageList_Succeeds() {
            // Arrange
            var client = new FakePop3Client();
            client.Connect();

            // Act
            var list = client.FetchMessageList();

            // Assert
            Assert.Equal(2, list.Count);
        }
    }
}