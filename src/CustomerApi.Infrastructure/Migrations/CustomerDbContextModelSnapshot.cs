using System;
using CustomerApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace CustomerApi.Infrastructure.Migrations;

[DbContext(typeof(CustomerDbContext))]
partial class CustomerDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "10.0.12");

        modelBuilder.Entity("CustomerApi.Domain.Customer", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("TEXT");

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("TEXT");

            b.Property<string>("Email")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("FirstName")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("LastName")
                .IsRequired()
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.HasIndex("Email")
                .IsUnique()
                .HasDatabaseName("IX_Customers_Email");

            b.ToTable("Customers");
        });
#pragma warning restore 612, 618
    }
}
