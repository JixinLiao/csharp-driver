//
//      Copyright (C) 2012-2014 DataStax Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
﻿using Moq;
﻿using NUnit.Framework;

namespace Cassandra.Tests
{
    [TestFixture]
    public class TokenTests
    {
        [Test]
        public void MurmurHashTest()
        {
            //inputs and result values from Cassandra
            var values = new Dictionary<byte[], M3PToken>()
            {
                {new byte[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16},                             new M3PToken(-5563837382979743776L)},
                {new byte[] {2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17},                            new M3PToken(-1513403162740402161L)},
                {new byte[] {3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18},                           new M3PToken(-495360443712684655L)},
                {new byte[] {4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19},                          new M3PToken(1734091135765407943L)},
                {new byte[] {5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20},                         new M3PToken(-3199412112042527988L)},
                {new byte[] {6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21},                        new M3PToken(-6316563938475080831L)},
                {new byte[] {7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22},                       new M3PToken(8228893370679682632L)},
                {new byte[] {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},                                    new M3PToken(5457549051747178710L)},
                {new byte[] {255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255},    new M3PToken(-2824192546314762522L)},
                {new byte[] {254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254},    new M3PToken(-833317529301936754)},
                {new byte[] {000, 001, 002, 003, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255},    new M3PToken(6463632673159404390L)},
                {new byte[] {254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254},    new M3PToken(-1672437813826982685L)},
                {new byte[] {254, 254, 254, 254},    new M3PToken(4566408979886474012L)},
                {new byte[] {0, 0, 0, 0},    new M3PToken(-3485513579396041028L)},
                {new byte[] {0, 1, 127, 127},    new M3PToken(6573459401642635627)},
                {new byte[] {0, 255, 255, 255},    new M3PToken(123573637386978882)},
                {new byte[] {255, 1, 2, 3},    new M3PToken(-2839127690952877842)},
                {new byte[] {000, 001, 002, 003, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255},    new M3PToken(6463632673159404390L)},
                {new byte[] {226, 231},    new M3PToken(-8582699461035929883L)},
                {new byte[] {226, 231, 226, 231, 226, 231, 1},    new M3PToken(2222373981930033306)},
            };
            var factory = new M3PToken.M3PTokenFactory();
            foreach (var kv in values)
            {
                Assert.AreEqual(kv.Value, factory.Hash(kv.Key));
            }
        }

        [Test]
        public void TokenMapSimpleStrategyWithKeyspaceTest()
        {
            var rp = new ConstantReconnectionPolicy(1);
            var tokensByHost = new Dictionary<Host, HashSet<string>>
            {
                { new Host(GetAddress("192.168.0.0"), rp), new HashSet<string>{"0"}},
                { new Host(GetAddress("192.168.0.1"), rp), new HashSet<string>{"10"}},
                { new Host(GetAddress("192.168.0.2"), rp), new HashSet<string>{"20"}}
            };
            const string strategy = ReplicationStrategies.SimpleStrategy;
            var keyspaces = new List<KeyspaceMetadata>
            {
                new KeyspaceMetadata(null, "ks1", true, strategy, new Dictionary<string, int> {{"replication_factor", 2}}),
                new KeyspaceMetadata(null, "ks2", true, strategy, new Dictionary<string, int> {{"replication_factor", 10}})
            };
            var tokenMap = TokenMap.Build("Murmur3Partitioner", tokensByHost, keyspaces);

            //the primary replica and the next
            var replicas = tokenMap.GetReplicas("ks1", new M3PToken(0));
            Assert.AreEqual("0,1", String.Join(",", replicas.Select(h => h.Address.ToString().Last())));

            //The next replica should be the first
            replicas = tokenMap.GetReplicas("ks1", new M3PToken(20));
            Assert.AreEqual("2,0", String.Join(",", replicas.Select(h => h.Address.ToString().Last())));

            //The closest replica and the next
            replicas = tokenMap.GetReplicas("ks1", new M3PToken(19));
            Assert.AreEqual("2,0", String.Join(",", replicas.Select(h => h.Address.ToString().Last())));

            //Even if the replication factor is greater than the ring, it should return only ring size
            replicas = tokenMap.GetReplicas("ks2", new M3PToken(5));
            Assert.AreEqual("1,2,0", String.Join(",", replicas.Select(h => h.Address.ToString().Last())));

            //The primary replica only as the keyspace was not found
            replicas = tokenMap.GetReplicas(null, new M3PToken(0));
            Assert.AreEqual("0", String.Join(",", replicas.Select(h => h.Address.ToString().Last())));
            replicas = tokenMap.GetReplicas(null, new M3PToken(10));
            Assert.AreEqual("1", String.Join(",", replicas.Select(h => h.Address.ToString().Last())));
            replicas = tokenMap.GetReplicas("ks_does_not_exist", new M3PToken(20));
            Assert.AreEqual("2", String.Join(",", replicas.Select(h => h.Address.ToString().Last())));
            replicas = tokenMap.GetReplicas(null, new M3PToken(19));
            Assert.AreEqual("2", String.Join(",", replicas.Select(h => h.Address.ToString().Last())));
        }

        private static IPAddress GetAddress(string address)
        {
            //To ease up the migration between IP to endpoint
            return IPAddress.Parse(address);
        }
    }
}
