/*
The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 */

// https://github.com/dotnet/tye/blob/main/src/Microsoft.Tye.Core/NextPortFinder.cs

using System.Net;
using System.Net.Sockets;

namespace http_client
{
    public sealed class NextPortFinder
    {
        public static int GetNextPort()
        {
            // Let the OS assign the next available port. Unless we cycle through all ports
            // on a test run, the OS will always increment the port number when making these calls.
            // This prevents races in parallel test runs where a test is already bound to
            // a given port, and a new test is able to bind to the same port due to port
            // reuse being enabled by default by the OS.
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)socket.LocalEndPoint!).Port;
        }

        // Alternative
        private static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            return port;
        }
    }
}
