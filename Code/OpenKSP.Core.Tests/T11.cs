using System;
using System.Collections.Generic;
using System.IO;
using OpenKSP.Core.Celestial;
using Xunit;

namespace OpenKSP.Core.Tests
{
    /// <summary>
    /// T11: Phase 2 Task 1 — parent hierarchy end-to-end.
    /// CBody.ParentId loads from §6 "parent"; unknown or self parents are
    /// rejected deterministically; WorldState.ParentIndex(i) resolves to the
    /// parent's body index (-1 for roots), O(1) and alloc-free.
    /// </summary>
    public class T11
    {
        private const string BodiesTemplatePrefix =
            "{ \"bodies\": [\n" +
            "  { \"id\": \"sun\",   \"name\": \"Sun\",   \"mass\": 1.989e30, \"radius\": 6.96e8, \"mu\": 1.32712440018e20, \"parent\": ";

        private static CBody GetByIdOrThrow(IReadOnlyList<CBody> bodies, string id)
        {
            var body = CBodyLoader.GetById(bodies, id);
            Assert.NotNull(body);
            return body.Value;
        }

        /// <summary>
        /// The shipped planets.json now carries real parents: sun→null,
        /// earth→sun, moon→earth. ParentId must survive the load.
        /// </summary>
        [Fact]
        public void PlanetsJsonCarriesParentHierarchy()
        {
            var bodies = CBodyLoader.Load();

            var sun = GetByIdOrThrow(bodies, "sun");
            var earth = GetByIdOrThrow(bodies, "earth");
            var moon = GetByIdOrThrow(bodies, "moon");

            Assert.Null(sun.ParentId);
            Assert.Equal("sun", earth.ParentId);
            Assert.Equal("earth", moon.ParentId);
        }

        /// <summary>
        /// WorldState resolves ParentIndex over the loaded set: -1 for the
        /// root, parent's list index otherwise. Indices follow file order.
        /// </summary>
        [Fact]
        public void ParentIndexResolvesLoadedHierarchy()
        {
            var bodies = CBodyLoader.Load();
            var world = new WorldState(bodies);

            Assert.Equal(-1, world.ParentIndex(0));         // sun (root)
            Assert.Equal(0, world.ParentIndex(1));          // earth → sun
            Assert.Equal(1, world.ParentIndex(2));          // moon → earth
        }

        /// <summary>
        /// Cycle in the parent hierarchy (a→b→a) is deterministic invalid.
        /// </summary>
        [Fact]
        public void CycleThrowsInvalidDataException()
        {
            var json = BodiesTemplatePrefix +
                "\"earth\" }," + "\n" +
                "  { \"id\": \"earth\", \"name\": \"Earth\", \"mass\": 5.972e24, \"radius\": 6.371e6, \"mu\": 3.986004418e14,  \"parent\": \"sun\" }" +
                " ] }";

            var bodies = CBodyLoader.Parse(json);
            Assert.Throws<InvalidDataException>(() => new WorldState(bodies));
        }

        /// <summary>
        /// A forest with two independent roots is valid: each root resolves
        /// to -1, and children resolve to their parent's index.
        /// </summary>
        [Fact]
        public void MultiRootForestIsValid()
        {
            var json = BodiesTemplatePrefix +
                "null }," + "\n" +
                "  { \"id\": \"earth\", \"name\": \"Earth\", \"mass\": 5.972e24, \"radius\": 6.371e6, \"mu\": 3.986004418e14,  \"parent\": \"sun\" }," + "\n" +
                "  { \"id\": \"jove\",  \"name\": \"Jupiter\", \"mass\": 1.898e27, \"radius\": 6.9911e7, \"mu\": 1.26686534e17, \"parent\": null }," + "\n" +
                "  { \"id\": \"io\",    \"name\": \"Io\",      \"mass\": 1.493e23, \"radius\": 1.8216e6, \"mu\": 5.7953e13,     \"parent\": \"jove\" }" +
                " ] }";

            var bodies = CBodyLoader.Parse(json);
            var world = new WorldState(bodies);

            Assert.Equal(-1, world.ParentIndex(0));          // sun (root)
            Assert.Equal(0, world.ParentIndex(1));          // earth → sun
            Assert.Equal(-1, world.ParentIndex(2));          // jove (root)
            Assert.Equal(2, world.ParentIndex(3));          // io → jove
        }

        /// <summary>
        /// Invalid hierarchy: a parent id that does not exist in the file.
        /// </summary>
        [Fact]
        public void UnknownParentThrowsInvalidDataException()
        {
            var json = BodiesTemplatePrefix +
                "null }," + "\n" +
                "  { \"id\": \"earth\", \"name\": \"Earth\", \"mass\": 5.972e24, \"radius\": 6.371e6, \"mu\": 3.986004418e14,  \"parent\": \"jupiter\" }" +
                " ] }";

            var bodies = CBodyLoader.Parse(json);
            Assert.Throws<InvalidDataException>(() => new WorldState(bodies));
        }

        /// <summary>
        /// Invalid hierarchy: a body that is its own parent.
        /// </summary>
        [Fact]
        public void SelfParentThrowsInvalidDataException()
        {
            var json = BodiesTemplatePrefix +
                "null }," + "\n" +
                "  { \"id\": \"earth\", \"name\": \"Earth\", \"mass\": 5.972e24, \"radius\": 6.371e6, \"mu\": 3.986004418e14,  \"parent\": \"earth\" }" +
                " ] }";

            var bodies = CBodyLoader.Parse(json);
            Assert.Throws<InvalidDataException>(() => new WorldState(bodies));
        }

        /// <summary>
        /// Duplicate ids (same id twice) must be rejected deterministically.
        /// </summary>
        [Fact]
        public void DuplicateIdThrowsInvalidDataException()
        {
            var json = BodiesTemplatePrefix +
                "null }," + "\n" +
                "  { \"id\": \"sun\", \"name\": \"Sun2\", \"mass\": 1.0, \"radius\": 1.0, \"mu\": 1.0, \"parent\": null }" +
                " ] }";

            Assert.Throws<InvalidDataException>(() => CBodyLoader.Parse(json));
        }

        /// <summary>
        /// Explicit null parent is accepted and treated the same as a
        /// missing parent (root body, ParentIndex = -1).
        /// </summary>
        [Fact]
        public void NullParentIsAccepted()
        {
            var json = BodiesTemplatePrefix +
                "null }," + "\n" +
                "  { \"id\": \"earth\", \"name\": \"Earth\", \"mass\": 5.972e24, \"radius\": 6.371e6, \"mu\": 3.986004418e14,  \"parent\": \"sun\" }" +
                " ] }";

            var bodies = CBodyLoader.Parse(json);
            Assert.Null(GetByIdOrThrow(bodies, "sun").ParentId);
            var world = new WorldState(bodies);
            Assert.Equal(-1, world.ParentIndex(0));
        }

        /// <summary>
        /// Non-string parent (a number) is a schema violation.
        /// </summary>
        [Fact]
        public void NonStringParentThrowsInvalidDataException()
        {
            var json = BodiesTemplatePrefix +
                "null }," + "\n" +
                "  { \"id\": \"earth\", \"name\": \"Earth\", \"mass\": 5.972e24, \"radius\": 6.371e6, \"mu\": 3.986004418e14,  \"parent\": 2 }" +
                " ] }";

            Assert.Throws<InvalidDataException>(() => CBodyLoader.Parse(json));
        }
    }
}
