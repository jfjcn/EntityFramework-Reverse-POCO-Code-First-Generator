// ------------------------------------------------------------------------------------------------
// This code was generated by EntityFramework Reverse POCO Generator (http://www.reversepoco.co.uk/).
// Created by Simon Hughes (https://about.me/simon.hughes).
//
// Registered to: Simon Hughes
// Company      : Reverse POCO
// Licence Type : Commercial
// Licences     : 1
// Valid until  : 03 NOV 2020
//
// Do not make changes directly to this file - edit the template instead.
//
// The following connection settings were used to generate this file:
//     Connection String Name: "McsfMultiDatabase"
//     Connection String:      "Data Source=(local);Initial Catalog=EfrpgTest;Integrated Security=True"
//     Multi-context settings: "Data Source=(local);Initial Catalog=EfrpgTest_Settings;Integrated Security=True"
// ------------------------------------------------------------------------------------------------
// Database Edition       : Developer Edition (64-bit)
// Database Engine Edition: Enterprise
// Database Version       : 14.0.2027.2

// <auto-generated>
// ReSharper disable CheckNamespace
// ReSharper disable ConvertPropertyToExpressionBody
// ReSharper disable DoNotCallOverridableMethodsInConstructor
// ReSharper disable EmptyNamespace
// ReSharper disable InconsistentNaming
// ReSharper disable NotAccessedVariable
// ReSharper disable PartialMethodWithSinglePart
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable RedundantCast
// ReSharper disable RedundantNameQualifier
// ReSharper disable RedundantOverridenMember
// ReSharper disable UseNameofExpression
// ReSharper disable UsePatternMatching
#pragma warning disable 1591    //  Ignore "Missing XML Comment" warning

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tester.Integration.EfCore3.Multi_context_single_filesAppleDbContext
{
    #region Database context interface

    public interface IAppleDbContext : IDisposable
    {
        DbSet<Stafford_Boo> Stafford_Boos { get; set; } // Boo
        DbSet<Stafford_Foo> Stafford_Foos { get; set; } // Foo

        int SaveChanges();
        int SaveChanges(bool acceptAllChangesOnSuccess);
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken));
        Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken));
        DatabaseFacade Database { get; }
        DbSet<TEntity> Set<TEntity>() where TEntity : class;
        string ToString();
    }

    #endregion

    #region Database context

    public class AppleDbContext : DbContext, IAppleDbContext
    {
        public AppleDbContext()
        {
        }

        public AppleDbContext(DbContextOptions<AppleDbContext> options)
            : base(options)
        {
        }

        public DbSet<Stafford_Boo> Stafford_Boos { get; set; } // Boo
        public DbSet<Stafford_Foo> Stafford_Foos { get; set; } // Foo

        public bool IsSqlParameterNull(SqlParameter param)
        {
            var sqlValue = param.SqlValue;
            var nullableValue = sqlValue as INullable;
            if (nullableValue != null)
                return nullableValue.IsNull;
            return (sqlValue == null || sqlValue == DBNull.Value);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(new Stafford_BooConfiguration());
            modelBuilder.ApplyConfiguration(new Stafford_FooConfiguration());
        }

    }

    #endregion

    #region Database context factory

    public class AppleDbContextFactory : IDesignTimeDbContextFactory<AppleDbContext>
    {
        public AppleDbContext CreateDbContext(string[] args)
        {
            return new AppleDbContext();
        }
    }

    #endregion

    #region POCO classes

    // Boo
    [CustomSecurity(SecurityEnum.Readonly)]
    public class Stafford_Boo
    {
        public int id { get; set; } // id (Primary key)
        public string Name { get; set; } // name (length: 10)

        // Reverse navigation

        /// <summary>
        /// Parent (One-to-One) Stafford_Boo pointed by [Foo].[id] (FK_Foo_Boo)
        /// </summary>
        public virtual Stafford_Foo Stafford_Foo { get; set; } // Foo.FK_Foo_Boo
    }

    // Foo
    public class Stafford_Foo
    {
        public int id { get; set; } // id (Primary key)

        // Foreign keys

        /// <summary>
        /// Parent Stafford_Boo pointed by [Foo].([id]) (FK_Foo_Boo)
        /// </summary>
        public virtual Stafford_Boo Stafford_Boo { get; set; } // FK_Foo_Boo
    }


    #endregion

    #region POCO Configuration

    // Boo
    public class Stafford_BooConfiguration : IEntityTypeConfiguration<Stafford_Boo>
    {
        public void Configure(EntityTypeBuilder<Stafford_Boo> builder)
        {
            builder.ToTable("Boo", "Stafford");
            builder.HasKey(x => x.id).HasName("PK_Boo").IsClustered();

            builder.Property(x => x.id).HasColumnName(@"id").HasColumnType("int").IsRequired().ValueGeneratedOnAdd().UseIdentityColumn();
            builder.Property(x => x.Name).HasColumnName(@"name").HasColumnType("nchar").IsRequired().IsFixedLength().HasMaxLength(10);
        }
    }

    // Foo
    public class Stafford_FooConfiguration : IEntityTypeConfiguration<Stafford_Foo>
    {
        public void Configure(EntityTypeBuilder<Stafford_Foo> builder)
        {
            builder.ToTable("Foo", "Stafford");
            builder.HasKey(x => x.id).HasName("PK_Foo").IsClustered();

            builder.Property(x => x.id).HasColumnName(@"id").HasColumnType("int").IsRequired().ValueGeneratedNever();

            // Foreign keys
            builder.HasOne(a => a.Stafford_Boo).WithOne(b => b.Stafford_Foo).HasForeignKey<Stafford_Foo>(c => c.id).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_Foo_Boo");
        }
    }


    #endregion

}
// </auto-generated>

