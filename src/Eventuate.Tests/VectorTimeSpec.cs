﻿#region copyright
// -----------------------------------------------------------------------
//  <copyright file="VectorTimeSpec.cs" company="Bartosz Sypytkowski">
//      Copyright (C) 2015-2019 Red Bull Media House GmbH <http://www.redbullmediahouse.com>
//      Copyright (C) 2019-2019 Bartosz Sypytkowski <b.sypytkowski@gmail.com>
//  </copyright>
// -----------------------------------------------------------------------
#endregion

using Xunit;
using FluentAssertions;

namespace Eventuate.Tests
{
    public class VectorTimeSpec
    {
        [Fact]
        public void A_VectorTime_must_have_zero_versions_when_created()
        {
            var clock = VectorTime.Zero;
            clock.Value.Should().BeEmpty();
        }

        [Fact]
        public void A_VectorTime_must_not_happen_before_itself()
        {
            var clock1 = VectorTime.Zero;
            var clock2 = VectorTime.Zero;

            (clock1 != clock2).Should().BeFalse();
        }

        [Fact]
        public void A_VectorTime_must_pass_misc_comparison_test1()
        {
            var clock1_1 = VectorTime.Zero;
            var clock2_1 = clock1_1.Increment("1");
            var clock3_1 = clock2_1.Increment("2");
            var clock4_1 = clock3_1.Increment("1");

            var clock1_2 = VectorTime.Zero;
            var clock2_2 = clock1_2.Increment("1");
            var clock3_2 = clock2_2.Increment("2");
            var clock4_2 = clock3_2.Increment("1");

            (clock4_1 != clock4_2).Should().BeFalse();
        }

        [Fact]
        public void A_VectorTime_must_pass_misc_comparison_test2()
        {
            var clock1_1 = VectorTime.Zero;
            var clock2_1 = clock1_1.Increment("1");
            var clock3_1 = clock2_1.Increment("2");
            var clock4_1 = clock3_1.Increment("1");

            var clock1_2 = VectorTime.Zero;
            var clock2_2 = clock1_2.Increment("1");
            var clock3_2 = clock2_2.Increment("2");
            var clock4_2 = clock3_2.Increment("1");
            var clock5_2 = clock4_2.Increment("3");

            (clock4_1 < clock5_2).Should().BeTrue();
        }

        [Fact]
        public void A_VectorTime_must_pass_misc_comparison_test3()
        {
            var clock1_1 = VectorTime.Zero;
            var clock2_1 = clock1_1.Increment("1");

            var clock1_2 = VectorTime.Zero;
            var clock2_2 = clock1_2.Increment("2");

            clock2_1.PartiallyCompareTo(clock2_2).Should().Be(null);
            (clock2_1 != clock2_2).Should().BeTrue();
        }

        [Fact]
        public void A_VectorTime_must_pass_misc_comparison_test4()
        {
            var clock1_3 = VectorTime.Zero;
            var clock2_3 = clock1_3.Increment("1");
            var clock3_3 = clock2_3.Increment("2");
            var clock4_3 = clock3_3.Increment("1");

            var clock1_4 = VectorTime.Zero;
            var clock2_4 = clock1_4.Increment("1");
            var clock3_4 = clock2_4.Increment("1");
            var clock4_4 = clock3_4.Increment("3");

            (clock4_3 != clock4_4).Should().BeTrue();
        }

        [Fact]
        public void A_VectorTime_must_pass_misc_comparison_test5()
        {
            var clock1_1 = VectorTime.Zero;
            var clock2_1 = clock1_1.Increment("2");
            var clock3_1 = clock2_1.Increment("2");

            var clock1_2 = VectorTime.Zero;
            var clock2_2 = clock1_2.Increment("1");
            var clock3_2 = clock2_2.Increment("2");
            var clock4_2 = clock3_2.Increment("2");
            var clock5_2 = clock4_2.Increment("3");

            (clock3_1 < clock5_2).Should().BeTrue();
            (clock5_2 > clock3_1).Should().BeTrue();
        }

        [Fact]
        public void A_VectorTime_must_pass_misc_comparison_test6()
        {
            var clock1_1 = VectorTime.Zero;
            var clock2_1 = clock1_1.Increment("1");
            var clock3_1 = clock2_1.Increment("2");

            var clock1_2 = VectorTime.Zero;
            var clock2_2 = clock1_2.Increment("1");
            var clock3_2 = clock2_2.Increment("1");

            (clock3_1 != clock3_2).Should().BeTrue();
            (clock3_2 != clock3_1).Should().BeTrue();
        }

        [Fact]
        public void A_VectorTime_must_pass_misc_comparison_test7()
        {
            var clock1_1 = VectorTime.Zero;
            var clock2_1 = clock1_1.Increment("1");
            var clock3_1 = clock2_1.Increment("2");
            var clock4_1 = clock3_1.Increment("2");
            var clock5_1 = clock4_1.Increment("3");

            var clock1_2 = clock4_1;
            var clock2_2 = clock1_2.Increment("2");
            var clock3_2 = clock2_2.Increment("2");

            (clock5_1 != clock3_2).Should().BeTrue();
            (clock3_2 != clock5_1).Should().BeTrue();
        }

        [Fact]
        public void A_VectorTime_must_pass_misc_comparison_test8()
        {
            var clock1_1 = VectorTime.Zero;
            var clock2_1 = clock1_1.Increment("1");
            var clock3_1 = clock2_1.Increment("3");

            var clock1_2 = clock3_1.Increment("2");

            var clock4_1 = clock3_1.Increment("3");

            (clock4_1 != clock1_2).Should().BeTrue();
            (clock1_2 != clock4_1).Should().BeTrue();
        }

        [Fact]
        public void A_VectorTime_must_correctly_merge_two_clocks()
        {
            var node1 = "1";
            var node2 = "2";
            var node3 = "3";

            var clock1_1 = VectorTime.Zero;
            var clock2_1 = clock1_1.Increment(node1);
            var clock3_1 = clock2_1.Increment(node2);
            var clock4_1 = clock3_1.Increment(node2);
            var clock5_1 = clock4_1.Increment(node3);

            var clock1_2 = clock4_1;
            var clock2_2 = clock1_2.Increment(node2);
            var clock3_2 = clock2_2.Increment(node2);

            var merged1 = clock3_2.Merge(clock5_1);
            merged1.Value.Count.Should().Be(3);
            merged1[node1].Should().Be(1);
            merged1[node2].Should().Be(4);
            merged1[node3].Should().Be(1);

            var merged2 = clock5_1.Merge(clock3_2);
            merged2.Value.Count.Should().Be(3);
            merged2[node1].Should().Be(1);
            merged2[node2].Should().Be(4);
            merged2[node3].Should().Be(1);

            (clock3_2 < merged1).Should().BeTrue();
            (clock5_1 < merged1).Should().BeTrue();

            (clock3_2 < merged2).Should().BeTrue();
            (clock5_1 < merged2).Should().BeTrue();

            (merged1 == merged2).Should().BeTrue();
        }

        [Fact]
        public void A_VectorTime_must_correctly_merge_two_disjoint_vector_clocks()
        {
            var node1 = "1";
            var node2 = "2";
            var node3 = "3";
            var node4 = "4";

            var clock1_1 = VectorTime.Zero;
            var clock2_1 = clock1_1.Increment(node1);
            var clock3_1 = clock2_1.Increment(node2);
            var clock4_1 = clock3_1.Increment(node2);
            var clock5_1 = clock4_1.Increment(node3);

            var clock1_2 = VectorTime.Zero;
            var clock2_2 = clock1_2.Increment(node4);
            var clock3_2 = clock2_2.Increment(node4);

            var merged1 = clock3_2.Merge(clock5_1);
            merged1.Value.Count.Should().Be(4);
            merged1[node1].Should().Be(1);
            merged1[node2].Should().Be(2);
            merged1[node3].Should().Be(1);
            merged1[node4].Should().Be(2);

            var merged2 = clock5_1.Merge(clock3_2);
            merged2.Value.Count.Should().Be(4);
            merged2[node1].Should().Be(1);
            merged2[node2].Should().Be(2);
            merged2[node3].Should().Be(1);
            merged2[node4].Should().Be(2);

            (clock3_2 < merged1).Should().BeTrue();
            (clock5_1 < merged1).Should().BeTrue();

            (clock3_2 < merged2).Should().BeTrue();
            (clock5_1 < merged2).Should().BeTrue();

            (merged1 == merged2).Should().BeTrue();
        }

        [Fact]
        public void A_VectorTime_must_pass_blank_clock_incrementing()
        {
            var node1 = "1";
            var node2 = "2";

            var v1 = VectorTime.Zero;
            var v2 = VectorTime.Zero;

            var vv1 = v1.Increment(node1);
            var vv2 = v2.Increment(node2);

            (vv1 > v1).Should().BeTrue();
            (vv2 > v2).Should().BeTrue();

            (vv1 > v2).Should().BeTrue();
            (vv2 > v1).Should().BeTrue();

            (vv2 > vv1).Should().BeFalse();
            (vv1 > vv2).Should().BeFalse();
        }

        [Fact]
        public void A_VectorTime_must_pass_merging_behavior()
        {
            var node1 = "1";
            var node2 = "2";
            var node3 = "3";

            var a = VectorTime.Zero;
            var b = VectorTime.Zero;

            var a1 = a.Increment(node1);
            var b1 = b.Increment(node2);

            var a2 = a1.Increment(node1);
            var c = a2.Merge(b1);
            var c1 = c.Increment(node3);

            (c1 > a2).Should().BeTrue();
            (c1 > b1).Should().BeTrue();
        }
    }
}
