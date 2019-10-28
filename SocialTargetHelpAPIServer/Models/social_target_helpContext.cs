using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace SocialTargetHelpAPIServer.Models
{
    public partial class social_target_helpContext : DbContext
    {
        public social_target_helpContext()
        {
        }

        public social_target_helpContext(DbContextOptions<social_target_helpContext> options)
            : base(options)
        {
        }

        public virtual DbSet<CdData> CdData { get; set; }
        public virtual DbSet<CdMetadata> CdMetadata { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. See http://go.microsoft.com/fwlink/?LinkId=723263 for guidance on storing connection strings.
                optionsBuilder.UseNpgsql("Server=192.168.2.179;Database=social_target_help;UID=postgres;PWD=motorhead33;");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("dblink")
                .HasPostgresExtension("pg_trgm")
                .HasAnnotation("ProductVersion", "2.2.0-rtm-35687");

            modelBuilder.Entity<CdData>(entity =>
            {
                entity.ToTable("cd_data", "api_req");

                entity.HasIndex(e => new { e.CDocumentSerial, e.CDocumentNumber })
                    .HasName("i_document")
                    .IsUnique();

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasDefaultValueSql("nextval('api_req.cd_date_id_seq'::regclass)");

                entity.Property(e => e.CDocumentNumber)
                    .HasColumnName("c_document_number")
                    .HasMaxLength(6);

                entity.Property(e => e.CDocumentSerial)
                    .HasColumnName("c_document_serial")
                    .HasMaxLength(4);

                entity.Property(e => e.CFirstName)
                    .IsRequired()
                    .HasColumnName("c_first_name")
                    .HasMaxLength(255);

                entity.Property(e => e.CLastName)
                    .IsRequired()
                    .HasColumnName("c_last_name")
                    .HasMaxLength(255);

                entity.Property(e => e.CMiddleName)
                    .IsRequired()
                    .HasColumnName("c_middle_name")
                    .HasMaxLength(255);

                entity.Property(e => e.DBirthDate)
                    .HasColumnName("d_birth_date")
                    .HasColumnType("date");
            });

            modelBuilder.Entity<CdMetadata>(entity =>
            {
                entity.ToTable("cd_metadata", "api_req");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .ValueGeneratedNever();

                entity.Property(e => e.CMethod)
                    .HasColumnName("c_method")
                    .HasMaxLength(255);

                entity.Property(e => e.CSenderCode)
                    .HasColumnName("c_sender_code")
                    .HasMaxLength(2000);
            });

            modelBuilder.HasSequence("cd_date_id_seq");
        }
    }
}
