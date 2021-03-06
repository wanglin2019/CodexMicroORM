﻿/***********************************************************************
Copyright 2018 CodeX Enterprises LLC

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

Major Changes:
12/2017    0.2.1   Initial release (Joel Champagne)
***********************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using CodexMicroORM.DemoObjects;
using Dapper;
using System.Data.SqlClient;
using System.Data;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows;

namespace CodexMicroORM.WPFDemo
{
    internal static class DapperBenchmarks
    {
        public static void Benchmark1(int total_parents, List<long> testTimes, List<long> testTimes2, ref int rowcount, ref int rowcount2)
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            long cnt1 = 0;

            string connstring = @"Data Source=(local)\sql2016;Database=CodexMicroORMTest;Integrated Security=SSPI;MultipleActiveResultSets=true";
            ConcurrentBag<PersonWrapped> people = new ConcurrentBag<PersonWrapped>();

            Parallel.For(1, total_parents + 1, (parentcnt) =>
            {
                using (IDbConnection db = new SqlConnection(connstring))
                {
                    var parent = new PersonWrapped() { Name = $"NP{parentcnt}", Age = (parentcnt % 60) + 20, Gender = (parentcnt % 2) == 0 ? "F" : "M", LastUpdatedBy = Environment.UserName, LastUpdatedDate = DateTime.UtcNow };
                    parent.PersonID = db.ExecuteScalar<int>("INSERT CEFTest.Person ([Name],Age,Gender,LastUpdatedBy,LastUpdatedDate) VALUES (@Name,@Age,@Gender,@LastUpdatedBy,@LastUpdatedDate); SELECT SCOPE_IDENTITY();", parent);

                    Interlocked.Add(ref cnt1, 4);

                    var ph1 = new Phone() { Number = "888-7777", PhoneTypeID = PhoneType.Mobile };
                    parent.Phones.Add(ph1);
                    db.Execute("INSERT CEFTest.Phone (Number,PhoneTypeID,PersonID,LastUpdatedBy,LastUpdatedDate) VALUES (@Number,@PhoneTypeID,@PersonID,@LastUpdatedBy,@LastUpdatedDate)", new { ph1.Number, PhoneTypeID = (int)ph1.PhoneTypeID, parent.PersonID, LastUpdatedBy = Environment.UserName, LastUpdatedDate = DateTime.UtcNow });

                    var ph2 = new Phone() { Number = "777-6666", PhoneTypeID = PhoneType.Work };
                    parent.Phones.Add(ph2);
                    db.Execute("INSERT CEFTest.Phone (Number,PhoneTypeID,PersonID,LastUpdatedBy,LastUpdatedDate) VALUES (@Number,@PhoneTypeID,@PersonID,@LastUpdatedBy,@LastUpdatedDate)", new { ph2.Number, PhoneTypeID = (int)ph2.PhoneTypeID, parent.PersonID, LastUpdatedBy = Environment.UserName, LastUpdatedDate = DateTime.UtcNow });

                    if ((parentcnt % 12) == 0)
                    {
                        db.Execute($"INSERT CEFTest.Phone (Number,PhoneTypeID,LastUpdatedBy,LastUpdatedDate) VALUES (@Number,@PhoneTypeID,@LastUpdatedBy,@LastUpdatedDate)", new { Number = "666-5555", PhoneTypeID = PhoneType.Home, LastUpdatedBy = Environment.UserName, LastUpdatedDate = DateTime.UtcNow });
                    }
                    else
                    {
                        var ph3 = new Phone() { Number = "777-6666", PhoneTypeID = PhoneType.Home };
                        parent.Phones.Add(ph3);
                        db.Execute("INSERT CEFTest.Phone (Number,PhoneTypeID,PersonID,LastUpdatedBy,LastUpdatedDate) VALUES (@Number,@PhoneTypeID,@PersonID,@LastUpdatedBy,@LastUpdatedDate)", new { ph3.Number, PhoneTypeID = (int)ph3.PhoneTypeID, parent.PersonID, LastUpdatedBy = Environment.UserName, LastUpdatedDate = DateTime.UtcNow });
                    }

                    for (int childcnt = 1; childcnt <= (parentcnt % 4); ++childcnt)
                    {
                        var child = new PersonWrapped() { Name = $"NC{parentcnt}{childcnt}", Age = parent.Age - 20, Gender = (parentcnt % 2) == 0 ? "F" : "M", LastUpdatedBy = Environment.UserName, LastUpdatedDate = DateTime.UtcNow, ParentPersonID = parent.PersonID };
                        child.PersonID = db.ExecuteScalar<int>("INSERT CEFTest.Person ([Name],Age,Gender,ParentPersonID,LastUpdatedBy,LastUpdatedDate) VALUES (@Name,@Age,@Gender,@ParentPersonID,@LastUpdatedBy,@LastUpdatedDate); SELECT SCOPE_IDENTITY();", child);
                        parent.Kids.Add(child);

                        var ph4 = new Phone() { Number = "999-8888", PhoneTypeID = PhoneType.Mobile };
                        child.Phones = new Phone[] { ph4 };
                        db.Execute("INSERT CEFTest.Phone (Number,PhoneTypeID,LastUpdatedBy,LastUpdatedDate) VALUES (@Number,@PhoneTypeID,@LastUpdatedBy,@LastUpdatedDate)", new { ph4.Number, PhoneTypeID = (int)ph4.PhoneTypeID, LastUpdatedBy = Environment.UserName, LastUpdatedDate = DateTime.UtcNow });

                        Interlocked.Add(ref cnt1, 2);
                    }

                    people.Add(parent);
                }
            });

            rowcount += (int)cnt1;
            testTimes.Add(watch.ElapsedMilliseconds);
            watch.Restart();
            long cnt2 = 0;

            // For everyone who's a parent of at least 30 yo, if at least 2 children of same sex, remove work phone, increment age
            using (IDbConnection db = new SqlConnection(connstring))
            {
                var people2 = db.Query("CEFTest.up_Person_SummaryForParents", new { RetVal = 1, Msg = "", MinimumAge = 30 }, commandType: CommandType.StoredProcedure);

                Parallel.ForEach((from d in people2 where d.MaleChildren > 1 || d.FemaleChildren > 1 select d).ToList(), (p) =>
                {
                    using (IDbConnection db2 = new SqlConnection(connstring))
                    {
                        p.Age += 1;
                        db2.Execute("UPDATE CEFTest.Person SET Age = @Age, LastUpdatedBy = @LastUpdatedBy, LastUpdatedDate = @LastUpdatedDate WHERE PersonID = @PersonID", new { p.PersonID, p.Age, LastUpdatedBy = Environment.UserName, LastUpdatedDate = DateTime.UtcNow });
                        Interlocked.Add(ref cnt2, 1);

                        var ph2 = db2.Query("CEFTest.up_Phone_ByPersonID", new { RetVal = 1, Msg = "", p.PersonID, PhoneTypeID = (int)PhoneType.Work }, commandType: CommandType.StoredProcedure).FirstOrDefault();

                        if (ph2 != null)
                        {
                            db2.Execute("DELETE CEFTest.Phone WHERE PhoneID=@PhoneID", new { ph2.PhoneID });
                            Interlocked.Add(ref cnt2, 1);
                        }
                    }
                });
            }

            rowcount2 += (int)cnt2;
            watch.Stop();
            testTimes2.Add(watch.ElapsedMilliseconds);
        }

        public static void Benchmark2(int total_parents, List<long> testTimes, List<long> testTimes2, ref int rowcount, ref int rowcount2)
        {
            CEFBenchmarks.Benchmark2Setup(total_parents);

            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            string connstring = @"Data Source=(local)\sql2016;Database=CodexMicroORMTest;Integrated Security=SSPI;MultipleActiveResultSets=true";
            SqlMapper.AddTypeMap(typeof(DateTime), DbType.DateTime2);

            // With caching enabled, we make 2 passes over the data where we a) use a query to get all parents (only), b) call a method that represents some API to get all phones for said parent, c) increment age on parents where they have a mobile phone, d) apply a possible update for the phones to modify their numbers based on another api method that only accepts a PhoneID (i.e. need to reretrieve some data)
            for (int j = 1; j <= 2; ++j)
            {
                using (IDbConnection db = new SqlConnection(connstring))
                {
                    var parents = db.Query("CEFTest.up_Person_SummaryForParents", new { RetVal = 1, Msg = "", MinimumAge = 30 }, commandType: CommandType.StoredProcedure);

                    foreach (var parent in parents)
                    {
                        var phones = db.Query("CEFTest.up_Phone_ByPersonID", new { RetVal = 1, Msg = "", parent.PersonID }, commandType: CommandType.StoredProcedure);
                        rowcount += 1;

                        if ((from a in phones where a.PhoneTypeID == (int)PhoneType.Mobile select a).Any())
                        {
                            parent.Age += 1;
                            parent.LastUpdatedBy = Environment.UserName;
                            int? ppid = parent.ParentPersonID;
                            string gender = parent.Gender;
                            db.Execute("CEFTest.up_Person_u", new { RetVal = 1, Msg = "", parent.PersonID, parent.Name, parent.Age, ParentPersonID = ppid, Gender = gender, parent.LastUpdatedBy, parent.LastUpdatedDate }, commandType: CommandType.StoredProcedure);
                            rowcount += 1;
                        }

                        foreach (var phone in phones)
                        {
                            string area = "";

                            switch ((PhoneType)phone.PhoneTypeID)
                            {
                                case PhoneType.Home:
                                    area = "707";
                                    break;

                                case PhoneType.Mobile:
                                    area = "415";
                                    break;

                                case PhoneType.Work:
                                    area = "800";
                                    break;
                            }

                            UpdatePhoneAPITest1(phone.PhoneID, area, ref rowcount);

                            if (!PhoneAPITest2(phone.PhoneID, parent.PersonID, ref rowcount))
                            {
                                throw new Exception("Failure!");
                            }
                        }
                    }
                }
            }

            watch.Stop();
            testTimes.Add(watch.ElapsedMilliseconds);

            // Extra verification that results match expected
            if (!CEFBenchmarks.Benchmark2Verify(total_parents))
            {
                throw new Exception("Unexpected final result.");
            }
        }

        private static void UpdatePhoneAPITest1(int phoneID, string area, ref int rowcount)
        {
            string connstring = @"Data Source=(local)\sql2016;Database=CodexMicroORMTest;Integrated Security=SSPI;MultipleActiveResultSets=true";

            using (IDbConnection db = new SqlConnection(connstring))
            {
                var phones = db.Query("CEFTest.up_Phone_ByKey", new { RetVal = 1, Msg = "", PhoneID = phoneID }, commandType: CommandType.StoredProcedure);
                var phone = phones.FirstOrDefault();
                rowcount += 1;

                if (phone != null)
                {
                    string oldNumber = phone.Number;

                    if (!string.IsNullOrEmpty(phone.Number) && (phone.Number.Length != 12 || !phone.Number.StartsWith(area)))
                    {
                        if (phone.Number.Length == 8)
                        {
                            phone.Number = area + "-" + phone.Number;
                        }
                        else
                        {
                            if (phone.Number.Length == 12)
                            {
                                phone.Number = area + "-" + phone.Number.Substring(4, 8);
                            }
                        }

                        if (oldNumber != phone.Number)
                        {
                            phone.LastUpdatedBy = Environment.UserName;
                            db.Execute("CEFTest.up_Phone_u", new
                            {
                                RetVal = 1,
                                Msg = "",
                                PhoneID = phoneID,
                                PhoneTypeID = (int)phone.PhoneTypeID,
                                phone.Number,
                                phone.PersonID,
                                phone.LastUpdatedBy,
                                phone.LastUpdatedDate
                            }, commandType: CommandType.StoredProcedure);

                            rowcount += 1;
                        }
                    }
                }
            }
        }

        private static bool PhoneAPITest2(int phoneID, int personID, ref int rowcount)
        {
            string connstring = @"Data Source=(local)\sql2016;Database=CodexMicroORMTest;Integrated Security=SSPI;MultipleActiveResultSets=true";

            using (IDbConnection db = new SqlConnection(connstring))
            {
                var phones = db.Query("CEFTest.up_Phone_ByKey", new { RetVal = 1, Msg = "", PhoneID = phoneID }, commandType: CommandType.StoredProcedure);
                var phone = phones.FirstOrDefault();
                rowcount += 1;

                if (phone != null)
                {
                    if (phone.PersonID == personID)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static void Benchmark3(int total_parents, List<long> testTimes, List<long> testTimes2, ref int rowcount, ref int rowcount2)
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            long cnt1 = 0;

            string connstring = @"Data Source=(local)\sql2016;Database=CodexMicroORMTest;Integrated Security=SSPI;MultipleActiveResultSets=true";
            ConcurrentBag<PersonWrapped> people = new ConcurrentBag<PersonWrapped>();

            Parallel.For(1, total_parents + 1, (parentcnt) =>
            {
                using (IDbConnection db = new SqlConnection(connstring))
                {
                    var parent = new PersonWrapped() { Name = $"NP{parentcnt}", Age = (parentcnt % 60) + 20, Gender = (parentcnt % 2) == 0 ? "F" : "M", LastUpdatedBy = Environment.UserName, LastUpdatedDate = DateTime.UtcNow };
                    parent.PersonID = db.ExecuteScalar<int>("INSERT CEFTest.Person ([Name],Age,Gender,LastUpdatedBy,LastUpdatedDate) VALUES (@Name,@Age,@Gender,@LastUpdatedBy,@LastUpdatedDate); SELECT SCOPE_IDENTITY();", parent);

                    Interlocked.Add(ref cnt1, 4);

                    var ph1 = new Phone() { Number = "888-7777", PhoneTypeID = PhoneType.Mobile };
                    parent.Phones.Add(ph1);
                    db.Execute("INSERT CEFTest.Phone (Number,PhoneTypeID,PersonID,LastUpdatedBy,LastUpdatedDate) VALUES (@Number,@PhoneTypeID,@PersonID,@LastUpdatedBy,@LastUpdatedDate)", new { ph1.Number, PhoneTypeID = (int)ph1.PhoneTypeID, parent.PersonID, LastUpdatedBy = Environment.UserName, LastUpdatedDate = DateTime.UtcNow });

                    var ph2 = new Phone() { Number = "777-6666", PhoneTypeID = PhoneType.Work };
                    parent.Phones.Add(ph2);
                    db.Execute("INSERT CEFTest.Phone (Number,PhoneTypeID,PersonID,LastUpdatedBy,LastUpdatedDate) VALUES (@Number,@PhoneTypeID,@PersonID,@LastUpdatedBy,@LastUpdatedDate)", new { ph2.Number, PhoneTypeID = (int)ph2.PhoneTypeID, parent.PersonID, LastUpdatedBy = Environment.UserName, LastUpdatedDate = DateTime.UtcNow });

                    if ((parentcnt % 12) == 0)
                    {
                        db.Execute($"INSERT CEFTest.Phone (Number,PhoneTypeID,LastUpdatedBy,LastUpdatedDate) VALUES (@Number,@PhoneTypeID,@LastUpdatedBy,@LastUpdatedDate)", new { Number = "666-5555", PhoneTypeID = PhoneType.Home, LastUpdatedBy = Environment.UserName, LastUpdatedDate = DateTime.UtcNow });
                    }
                    else
                    {
                        var ph3 = new Phone() { Number = "777-6666", PhoneTypeID = PhoneType.Home };
                        parent.Phones.Add(ph3);
                        db.Execute("INSERT CEFTest.Phone (Number,PhoneTypeID,PersonID,LastUpdatedBy,LastUpdatedDate) VALUES (@Number,@PhoneTypeID,@PersonID,@LastUpdatedBy,@LastUpdatedDate)", new { ph3.Number, PhoneTypeID = (int)ph3.PhoneTypeID, parent.PersonID, LastUpdatedBy = Environment.UserName, LastUpdatedDate = DateTime.UtcNow });
                    }

                    for (int childcnt = 1; childcnt <= (parentcnt % 4); ++childcnt)
                    {
                        var child = new PersonWrapped() { Name = $"NC{parentcnt}{childcnt}", Age = parent.Age - 20, Gender = (parentcnt % 2) == 0 ? "F" : "M", LastUpdatedBy = Environment.UserName, LastUpdatedDate = DateTime.UtcNow, ParentPersonID = parent.PersonID };
                        child.PersonID = db.ExecuteScalar<int>("INSERT CEFTest.Person ([Name],Age,Gender,ParentPersonID,LastUpdatedBy,LastUpdatedDate) VALUES (@Name,@Age,@Gender,@ParentPersonID,@LastUpdatedBy,@LastUpdatedDate); SELECT SCOPE_IDENTITY();", child);
                        parent.Kids.Add(child);

                        var ph4 = new Phone() { Number = "999-8888", PhoneTypeID = PhoneType.Mobile };
                        child.Phones = new Phone[] { ph4 };
                        db.Execute("INSERT CEFTest.Phone (Number,PhoneTypeID,LastUpdatedBy,LastUpdatedDate) VALUES (@Number,@PhoneTypeID,@LastUpdatedBy,@LastUpdatedDate)", new { ph4.Number, PhoneTypeID = (int)ph4.PhoneTypeID, LastUpdatedBy = Environment.UserName, LastUpdatedDate = DateTime.UtcNow });

                        Interlocked.Add(ref cnt1, 2);
                    }

                    people.Add(parent);
                }
            });

            rowcount += (int)cnt1;
            testTimes.Add(watch.ElapsedMilliseconds);
            watch.Restart();
            long cnt2 = 0;

            // For everyone who's a parent of at least 30 yo, if at least 2 children of same sex, remove work phone, increment age
            using (IDbConnection db = new SqlConnection(connstring))
            {
                int? id = null;

                var people2 = db.Query("CEFTest.up_Person_SummaryForParents", new { RetVal = 1, Msg = "", MinimumAge = 30 }, commandType: CommandType.StoredProcedure);

                Parallel.ForEach((from d in people2 where d.MaleChildren > 1 || d.FemaleChildren > 1 select d).ToList(), (p) =>
                {
                    using (IDbConnection db2 = new SqlConnection(connstring))
                    {
                        if (!id.HasValue)
                        {
                            id = p.PersonID;
                        }

                        p.Age += 1;
                        db2.Execute("UPDATE CEFTest.Person SET Age = @Age, LastUpdatedBy = @LastUpdatedBy, LastUpdatedDate = @LastUpdatedDate WHERE PersonID = @PersonID", new { p.PersonID, p.Age, LastUpdatedBy = Environment.UserName, LastUpdatedDate = DateTime.UtcNow });
                        Interlocked.Add(ref cnt2, 1);

                        var ph2 = db2.Query("CEFTest.up_Phone_ByPersonID", new { RetVal = 1, Msg = "", p.PersonID, PhoneTypeID = (int)PhoneType.Work }, commandType: CommandType.StoredProcedure).FirstOrDefault();

                        if (ph2 != null)
                        {
                            db2.Execute("DELETE CEFTest.Phone WHERE PhoneID=@PhoneID", new { ph2.PhoneID });
                            Interlocked.Add(ref cnt2, 1);
                        }
                    }
                });

                // Simulate "later heavy read access"...
                for (int c = 0; c < 50000; ++c)
                {
                    using (IDbConnection db2 = new SqlConnection(connstring))
                    {
                        var pid = c + id.GetValueOrDefault();
                        var work = db2.Query("CEFTest.up_Phone_ByPersonID", new { RetVal = 1, Msg = "", PersonID = pid, PhoneTypeID = (int)PhoneType.Work }, commandType: CommandType.StoredProcedure).FirstOrDefault();

                        if (work != null && work.Number == "123")
                        {
                            MessageBox.Show("Found (should never!)");
                        }
                    }
                }
            }

            rowcount2 += (int)cnt2 + 50000;
            watch.Stop();
            testTimes2.Add(watch.ElapsedMilliseconds);
        }
    }
}
