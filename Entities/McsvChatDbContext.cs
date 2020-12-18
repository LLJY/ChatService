using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace ChatService.Entities
{
    public partial class McsvChatDbContext : DbContext
    {

        public McsvChatDbContext(DbContextOptions<McsvChatDbContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Group> Groups { get; set; }
        public virtual DbSet<GroupMember> GroupMembers { get; set; }
        public virtual DbSet<Media> Medias { get; set; }
        public virtual DbSet<Message> Messages { get; set; }
        public virtual DbSet<UsersRead> UsersReads { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
          
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Group>(entity =>
            {
                entity.HasIndex(e => e.PhotoRef, "fk_Group_Media1_idx");

                entity.Property(e => e.Id)
                    .HasColumnType("int(11)")
                    .HasColumnName("id");

                entity.Property(e => e.PhotoRef)
                    .HasColumnType("int(11)")
                    .HasColumnName("photo_ref");

                entity.Property(e => e.Title)
                    .IsRequired()
                    .HasColumnType("varchar(45)")
                    .HasColumnName("title")
                    .HasCharSet("utf8mb4")
                    .HasCollation("utf8mb4_unicode_ci");

                entity.Property(e => e.Uuid)
                    .HasColumnName("uuid")
                    .HasCharSet("utf8mb4")
                    .HasCollation("utf8mb4_unicode_ci");

                entity.HasOne(d => d.PhotoRefNavigation)
                    .WithMany(p => p.Groups)
                    .HasForeignKey(d => d.PhotoRef)
                    .HasConstraintName("fk_Group_Media1");
            });

            modelBuilder.Entity<GroupMember>(entity =>
            {
                entity.HasIndex(e => e.GroupRef, "fk_GroupMembers_Groups1_idx");

                entity.Property(e => e.Id)
                    .HasColumnType("int(11)")
                    .HasColumnName("id");

                entity.Property(e => e.GroupRef)
                    .HasColumnType("int(11)")
                    .HasColumnName("group_ref");

                entity.Property(e => e.IsAdmin).HasColumnName("is_admin");

                entity.Property(e => e.Userid)
                    .IsRequired()
                    .HasColumnType("varchar(255)")
                    .HasColumnName("userid")
                    .HasCharSet("utf8mb4")
                    .HasCollation("utf8mb4_unicode_ci");

                entity.HasOne(d => d.GroupRefNavigation)
                    .WithMany(p => p.GroupMembers)
                    .HasForeignKey(d => d.GroupRef)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("fk_GroupMembers_Groups1");
            });

            modelBuilder.Entity<Media>(entity =>
            {
                entity.Property(e => e.Id)
                    .HasColumnType("int(11)")
                    .HasColumnName("id");

                entity.Property(e => e.MimeType)
                    .IsRequired()
                    .HasColumnType("varchar(255)")
                    .HasColumnName("mime_type")
                    .HasCharSet("utf8mb4")
                    .HasCollation("utf8mb4_unicode_ci");

                entity.Property(e => e.Size)
                    .HasColumnType("int(11)")
                    .HasColumnName("size");

                entity.Property(e => e.Url)
                    .IsRequired()
                    .HasColumnType("varchar(255)")
                    .HasColumnName("url")
                    .HasCharSet("utf8mb4")
                    .HasCollation("utf8mb4_unicode_ci");

                entity.Property(e => e.Uuid)
                    .HasColumnName("uuid")
                    .HasCharSet("utf8mb4")
                    .HasCollation("utf8mb4_unicode_ci");
            });

            modelBuilder.Entity<Message>(entity =>
            {
                entity.HasIndex(e => e.GroupRef, "fk_Message_Group1_idx");

                entity.HasIndex(e => e.MediaRef, "fk_Message_Media1_idx");

                entity.HasIndex(e => e.ReplyMessageRef, "fk_Message_Message1_idx");

                entity.Property(e => e.Id)
                    .HasColumnType("int(11)")
                    .HasColumnName("id");

                entity.Property(e => e.AuthorId)
                    .IsRequired()
                    .HasColumnType("varchar(255)")
                    .HasColumnName("author_id")
                    .HasCharSet("utf8mb4")
                    .HasCollation("utf8mb4_unicode_ci");

                entity.Property(e => e.Dateposted)
                    .HasColumnType("timestamp")
                    .ValueGeneratedOnAddOrUpdate()
                    .HasColumnName("dateposted")
                    .HasDefaultValueSql("current_timestamp()");

                entity.Property(e => e.GroupRef)
                    .HasColumnType("int(11)")
                    .HasColumnName("group_ref");

                entity.Property(e => e.IsForward).HasColumnName("is_forward");

                entity.Property(e => e.MediaRef)
                    .HasColumnType("int(11)")
                    .HasColumnName("media_ref");

                entity.Property(e => e.ReceiverId)
                    .HasColumnType("varchar(255)")
                    .HasColumnName("receiver_id")
                    .HasCharSet("utf8mb4")
                    .HasCollation("utf8mb4_unicode_ci");

                entity.Property(e => e.ReplyMessageRef)
                    .HasColumnType("int(11)")
                    .HasColumnName("reply_message_ref");

                entity.Property(e => e.Text)
                    .HasColumnType("text")
                    .HasColumnName("text")
                    .HasCharSet("utf8mb4")
                    .HasCollation("utf8mb4_unicode_ci");

                entity.Property(e => e.Uuid)
                    .HasColumnName("uuid")
                    .HasCharSet("utf8mb4")
                    .HasCollation("utf8mb4_unicode_ci");

                entity.HasOne(d => d.GroupRefNavigation)
                    .WithMany(p => p.Messages)
                    .HasForeignKey(d => d.GroupRef)
                    .HasConstraintName("fk_Message_Group1");

                entity.HasOne(d => d.MediaRefNavigation)
                    .WithMany(p => p.Messages)
                    .HasForeignKey(d => d.MediaRef)
                    .HasConstraintName("fk_Message_Media1");

                entity.HasOne(d => d.ReplyMessageRefNavigation)
                    .WithMany(p => p.InverseReplyMessageRefNavigation)
                    .HasForeignKey(d => d.ReplyMessageRef)
                    .HasConstraintName("fk_Message_Message1");
            });

            modelBuilder.Entity<UsersRead>(entity =>
            {
                entity.ToTable("UsersRead");

                entity.HasIndex(e => e.MessageRef, "fk_UsersRead_Message1_idx");

                entity.Property(e => e.Id)
                    .HasColumnType("int(11)")
                    .HasColumnName("id");

                entity.Property(e => e.MessageRef)
                    .HasColumnType("int(11)")
                    .HasColumnName("message_ref");

                entity.Property(e => e.MessageStatus)
                    .HasColumnType("int(11)")
                    .HasColumnName("message_status");

                entity.Property(e => e.UserId)
                    .IsRequired()
                    .HasColumnType("varchar(255)")
                    .HasColumnName("user_id")
                    .HasCharSet("utf8mb4")
                    .HasCollation("utf8mb4_unicode_ci");

                entity.HasOne(d => d.MessageRefNavigation)
                    .WithMany(p => p.UsersReads)
                    .HasForeignKey(d => d.MessageRef)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("fk_UsersRead_Message1");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
