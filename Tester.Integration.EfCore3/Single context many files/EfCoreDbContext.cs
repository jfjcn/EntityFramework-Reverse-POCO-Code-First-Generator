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
//     Connection String Name: "EfCoreDatabase"
//     Connection String:      "Data Source=(local);Initial Catalog=EfrpgTest;Integrated Security=True"
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
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tester.Integration.EfCore3.Single_context_many_files
{
    public class EfCoreDbContext : DbContext, IEfCoreDbContext
    {
        private readonly IConfiguration _configuration;

        public EfCoreDbContext()
        {
        }

        public EfCoreDbContext(DbContextOptions<EfCoreDbContext> options)
            : base(options)
        {
        }

        public EfCoreDbContext(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public DbSet<ColumnName> ColumnNames { get; set; } // ColumnNames
        public DbSet<Stafford_Boo> Stafford_Boos { get; set; } // Boo
        public DbSet<Stafford_ComputedColumn> Stafford_ComputedColumns { get; set; } // ComputedColumns
        public DbSet<Stafford_Foo> Stafford_Foos { get; set; } // Foo
        public DbSet<Synonyms_Child> Synonyms_Children { get; set; } // Child
        public DbSet<Synonyms_Parent> Synonyms_Parents { get; set; } // Parent
        public DbSet<UserInfo> UserInfoes { get; set; } // UserInfo
        public DbSet<UserInfoAttribute> UserInfoAttributes { get; set; } // UserInfoAttributes

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured && _configuration != null)
            {
                optionsBuilder.UseSqlServer(_configuration.GetConnectionString(@"EfCoreDatabase"));
            }
        }

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

            modelBuilder.ApplyConfiguration(new ColumnNameConfiguration());
            modelBuilder.ApplyConfiguration(new Stafford_BooConfiguration());
            modelBuilder.ApplyConfiguration(new Stafford_ComputedColumnConfiguration());
            modelBuilder.ApplyConfiguration(new Stafford_FooConfiguration());
            modelBuilder.ApplyConfiguration(new Synonyms_ChildConfiguration());
            modelBuilder.ApplyConfiguration(new Synonyms_ParentConfiguration());
            modelBuilder.ApplyConfiguration(new UserInfoConfiguration());
            modelBuilder.ApplyConfiguration(new UserInfoAttributeConfiguration());

            modelBuilder.Entity<Synonyms_SimpleStoredProcReturnModel>().HasNoKey();

            // Table Valued Functions
            modelBuilder.Entity<CsvToIntReturnModel>().HasNoKey();
        }


        // Stored Procedures
        public List<Synonyms_SimpleStoredProcReturnModel> Synonyms_SimpleStoredProc(int? inputInt)
        {
            int procResult;
            return Synonyms_SimpleStoredProc(inputInt, out procResult);
        }

        public List<Synonyms_SimpleStoredProcReturnModel> Synonyms_SimpleStoredProc(int? inputInt, out int procResult)
        {
            var inputIntParam = new SqlParameter { ParameterName = "@InputInt", SqlDbType = SqlDbType.Int, Direction = ParameterDirection.Input, Value = inputInt.GetValueOrDefault(), Precision = 10, Scale = 0 };
            if (!inputInt.HasValue)
                inputIntParam.Value = DBNull.Value;

            var procResultParam = new SqlParameter { ParameterName = "@procResult", SqlDbType = SqlDbType.Int, Direction = ParameterDirection.Output };
            const string sqlCommand = "EXEC @procResult = [Synonyms].[SimpleStoredProc] @InputInt";
            var procResultData = Set<Synonyms_SimpleStoredProcReturnModel>()
                .FromSqlRaw(sqlCommand, inputIntParam, procResultParam)
                .ToList();

            procResult = (int) procResultParam.Value;
            return procResultData;
        }

        public async Task<List<Synonyms_SimpleStoredProcReturnModel>> Synonyms_SimpleStoredProcAsync(int? inputInt)
        {
            var inputIntParam = new SqlParameter { ParameterName = "@InputInt", SqlDbType = SqlDbType.Int, Direction = ParameterDirection.Input, Value = inputInt.GetValueOrDefault(), Precision = 10, Scale = 0 };
            if (!inputInt.HasValue)
                inputIntParam.Value = DBNull.Value;

            const string sqlCommand = "EXEC [Synonyms].[SimpleStoredProc] @InputInt";
            var procResultData = await Set<Synonyms_SimpleStoredProcReturnModel>()
                .FromSqlRaw(sqlCommand, inputIntParam)
                .ToListAsync();

            return procResultData;
        }


        // Table Valued Functions

        // dbo.CsvToInt
        public IQueryable<CsvToIntReturnModel> CsvToInt(string array, string array2)
        {
            return Set<CsvToIntReturnModel>()
                .FromSqlRaw("SELECT * FROM [CsvToInt]({0}, {1})", array, array2)
                .AsNoTracking();
        }

        // Scalar Valued Functions

        [DbFunction("udfNetSale", "dbo")]
        public decimal UdfNetSale(int? quantity, decimal? listPrice, decimal? discount)
        {
            throw new Exception("Don't call this directly. Use LINQ to call the scalar valued function as part of your query");
        }
    }
}
// </auto-generated>

