﻿// -----------------------------------------------------------------------
//  <copyright file="RavenDB_3462.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------
using System.Globalization;
using System.Linq;
using Raven.Abstractions.Indexing;
using Raven.Client.Indexes;
using Raven.Tests.Common;
using Xunit;

namespace Raven.Tests.Issues
{
	public class RavenDB_3462 : RavenTest
	{
		[Fact]
		public void BoundingBoxIndexSparialSearch()
		{
			using (var documentStore = NewDocumentStore())
			{
				new EntitySpatialIndex().Execute(documentStore);
				new EntitySpatialIndex2().Execute(documentStore);

				var entity = new Entity
				{
					Geolocation = new Geolocation() { Lon = 12.559509, Lat = 55.673981 }, // POINT(12.559509 55.673981)
					Id = "fooid"
				};

				using (var session = documentStore.OpenSession())
				{
					session.Store(entity);
					session.SaveChanges();

					WaitForIndexing(documentStore);

					//Point(12.556675672531128 55.675285554217), corner of the bounding rectangle below
					var nearbyPoints = session.Query<Entity, EntitySpatialIndex>()
						.Customize(x =>
							x.WithinRadiusOf(fieldName: "Coordinates", radius: 1, latitude: 55.675285554217, longitude: 12.556675672531128))
						.ToList();

					Assert.Equal(1, nearbyPoints.Count); // Passes

					nearbyPoints = session.Query<Entity, EntitySpatialIndex2>()
						.Customize(x =>
							x.WithinRadiusOf(fieldName: "Coordinates", radius: 1, latitude: 55.675285554217, longitude: 12.556675672531128))
						.ToList();

					Assert.Equal(1, nearbyPoints.Count);

					var boundingRectangleWKT =
						"POLYGON((12.556675672531128 55.675285554217,12.56213665008545 55.675285554217,12.56213665008545 55.67261750095371,12.556675672531128 55.67261750095371,12.556675672531128 55.675285554217))";

					var q = session.Query<Entity, EntitySpatialIndex>()
						.Customize(x => x.RelatesToShape("Coordinates", boundingRectangleWKT, SpatialRelation.Within))
						.ToList();

					Assert.Equal(1, q.Count);

					q = session.Query<Entity, EntitySpatialIndex2>()
						.Customize(x => x.RelatesToShape("Coordinates", boundingRectangleWKT, SpatialRelation.Within))
						.ToList();

					Assert.Equal(1, q.Count); // does not pass
				}
			}
		}
	}

	public class Entity
	{
		public string Id { get; set; }
		public Geolocation Geolocation { get; set; }
	}

	public class Geolocation
	{
		public double Lon { get; set; }
		public double Lat { get; set; }
		public string WKT
		{
			get
			{
				return string.Format("POINT({0} {1})",
					Lon.ToString(CultureInfo.InvariantCulture),
					Lat.ToString(CultureInfo.InvariantCulture));
			}
		}
	}

	public class EntitySpatialIndex : AbstractIndexCreationTask<Entity>
	{
		public EntitySpatialIndex()
		{
			Map = entities => entities.Select(entity => new
			{
				entity.Id,
				Coordinates = entity.Geolocation.WKT
			});

			Spatial("Coordinates", x => x.Cartesian.BoundingBoxIndex());
		}
	}

	public class EntitySpatialIndex2 : AbstractIndexCreationTask<Entity>
	{
		public EntitySpatialIndex2()
		{
			Map = entities => entities.Select(e => new
			{
				Id = e.Id,
				__ = SpatialGenerate("Coordinates", e.Geolocation.Lat, e.Geolocation.Lon)
			});

			Spatial("Coordinates", x => x.Cartesian.BoundingBoxIndex());
		}
	}
}