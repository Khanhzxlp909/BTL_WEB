IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(50) NOT NULL,
        [NormalizedName] nvarchar(50) NOT NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(100) NULL,
        [IsActive] bit NOT NULL,
        [LockReason] nvarchar(500) NULL,
        [LockedByUserId] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LockedAt] datetime2 NULL,
        [UserName] nvarchar(50) NOT NULL,
        [NormalizedUserName] nvarchar(50) NOT NULL,
        [Email] nvarchar(254) NOT NULL,
        [NormalizedEmail] nvarchar(254) NOT NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] varchar(20) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Users_Lock] CHECK ([IsActive] = 1 OR ([LockReason] IS NOT NULL AND [LockReason] <> '' AND [LockedByUserId] IS NOT NULL AND [LockedAt] IS NOT NULL)),
        CONSTRAINT [FK_AspNetUsers_AspNetUsers_LockedByUserId] FOREIGN KEY ([LockedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE TABLE [SystemSettings] (
        [Id] int NOT NULL IDENTITY,
        [SettingKey] varchar(50) NOT NULL,
        [SettingValue] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_SystemSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] int NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] int NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] int NOT NULL,
        [RoleId] int NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] int NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE TABLE [Carts] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Carts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Carts_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE TABLE [Orders] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [OrderCode] varchar(30) NOT NULL,
        [Status] varchar(20) NOT NULL,
        [PaymentMethod] varchar(20) NOT NULL,
        [PaymentStatus] varchar(20) NOT NULL,
        [RecipientName] nvarchar(100) NOT NULL,
        [Phone] varchar(20) NOT NULL,
        [ShippingAddress] nvarchar(255) NOT NULL,
        [Note] nvarchar(500) NULL,
        [Subtotal] decimal(18,0) NOT NULL,
        [ShippingFee] decimal(18,0) NOT NULL,
        [TotalAmount] decimal(18,0) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [DeliveredAt] datetime2 NULL,
        [CancelledAt] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Orders_Cancelled] CHECK (([Status] = 'CANCELLED' AND [CancelledAt] IS NOT NULL) OR ([Status] <> 'CANCELLED' AND [CancelledAt] IS NULL)),
        CONSTRAINT [CK_Orders_Payment] CHECK ([PaymentMethod] = 'COD' AND (([Status] = 'DELIVERED' AND [PaymentStatus] = 'PAID' AND [DeliveredAt] IS NOT NULL) OR ([Status] <> 'DELIVERED' AND [PaymentStatus] = 'UNPAID' AND [DeliveredAt] IS NULL))),
        CONSTRAINT [CK_Orders_Status] CHECK ([Status] IN ('PENDING','CONFIRMED','SHIPPING','DELIVERED','CANCELLED')),
        CONSTRAINT [CK_Orders_Total] CHECK ([Subtotal] >= 0 AND [ShippingFee] = 0 AND [TotalAmount] = [Subtotal] + [ShippingFee]),
        CONSTRAINT [FK_Orders_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE TABLE [Books] (
        [Id] int NOT NULL IDENTITY,
        [CategoryId] int NOT NULL,
        [Title] nvarchar(255) NOT NULL,
        [Author] nvarchar(150) NOT NULL,
        [Publisher] nvarchar(150) NULL,
        [ISBN] varchar(30) NULL,
        [SKU] varchar(64) NOT NULL,
        [PublishedYear] int NULL,
        [Price] decimal(18,0) NOT NULL,
        [DiscountPrice] decimal(18,0) NULL,
        [Description] nvarchar(max) NULL,
        [CoverImage] nvarchar(500) NULL,
        [IsDeleted] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Books] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Books_Price] CHECK ([Price] > 0 AND ([DiscountPrice] IS NULL OR ([DiscountPrice] > 0 AND [DiscountPrice] <= [Price]))),
        CONSTRAINT [CK_Books_Year] CHECK ([PublishedYear] IS NULL OR [PublishedYear] >= 1),
        CONSTRAINT [FK_Books_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE TABLE [OrderEvents] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [ActorUserId] int NOT NULL,
        [FromStatus] varchar(20) NULL,
        [ToStatus] varchar(20) NOT NULL,
        [Note] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_OrderEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_OrderEvents_Status] CHECK ([ToStatus] IN ('PENDING','CONFIRMED','SHIPPING','DELIVERED','CANCELLED') AND ([FromStatus] IS NULL OR [FromStatus] IN ('PENDING','CONFIRMED','SHIPPING','DELIVERED','CANCELLED'))),
        CONSTRAINT [FK_OrderEvents_AspNetUsers_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OrderEvents_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE TABLE [CartItems] (
        [Id] int NOT NULL IDENTITY,
        [CartId] int NOT NULL,
        [BookId] int NOT NULL,
        [Quantity] int NOT NULL,
        CONSTRAINT [PK_CartItems] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CartItems_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [FK_CartItems_Books_BookId] FOREIGN KEY ([BookId]) REFERENCES [Books] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CartItems_Carts_CartId] FOREIGN KEY ([CartId]) REFERENCES [Carts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE TABLE [Inventories] (
        [Id] int NOT NULL IDENTITY,
        [BookId] int NOT NULL,
        [Quantity] int NOT NULL,
        [ReservedQuantity] int NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Inventories] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Inventories_Quantity] CHECK ([Quantity] >= 0 AND [ReservedQuantity] >= 0 AND [ReservedQuantity] <= [Quantity]),
        CONSTRAINT [FK_Inventories_Books_BookId] FOREIGN KEY ([BookId]) REFERENCES [Books] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE TABLE [OrderItems] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [BookId] int NOT NULL,
        [BookTitle] nvarchar(255) NOT NULL,
        [SKU] varchar(64) NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,0) NOT NULL,
        [LineTotal] AS [Quantity] * [UnitPrice] PERSISTED,
        CONSTRAINT [PK_OrderItems] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_OrderItems_Value] CHECK ([Quantity] > 0 AND [UnitPrice] > 0),
        CONSTRAINT [FK_OrderItems_Books_BookId] FOREIGN KEY ([BookId]) REFERENCES [Books] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OrderItems_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE TABLE [Reviews] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [BookId] int NOT NULL,
        [Rating] int NOT NULL,
        [Comment] nvarchar(1000) NOT NULL,
        [IsHidden] bit NOT NULL,
        [ModeratedByUserId] int NULL,
        [ModeratedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Reviews] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Reviews_Moderation] CHECK ([IsHidden] = 0 OR ([ModeratedByUserId] IS NOT NULL AND [ModeratedAt] IS NOT NULL)),
        CONSTRAINT [CK_Reviews_Rating] CHECK ([Rating] >= 1 AND [Rating] <= 5 AND [Comment] <> ''),
        CONSTRAINT [FK_Reviews_AspNetUsers_ModeratedByUserId] FOREIGN KEY ([ModeratedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reviews_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reviews_Books_BookId] FOREIGN KEY ([BookId]) REFERENCES [Books] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE TABLE [InventoryTransactions] (
        [Id] int NOT NULL IDENTITY,
        [InventoryId] int NOT NULL,
        [OrderId] int NULL,
        [ActorUserId] int NOT NULL,
        [Type] varchar(30) NOT NULL,
        [QuantityDelta] int NOT NULL,
        [ReservedDelta] int NOT NULL,
        [QuantityBefore] int NOT NULL,
        [QuantityAfter] int NOT NULL,
        [ReservedBefore] int NOT NULL,
        [ReservedAfter] int NOT NULL,
        [Reference] nvarchar(64) NULL,
        [Reason] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_InventoryTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_InventoryTransactions_Balance] CHECK ([QuantityAfter] = [QuantityBefore] + [QuantityDelta] AND [ReservedAfter] = [ReservedBefore] + [ReservedDelta] AND [QuantityBefore] >= 0 AND [QuantityAfter] >= 0 AND [ReservedBefore] >= 0 AND [ReservedAfter] >= 0 AND [ReservedBefore] <= [QuantityBefore] AND [ReservedAfter] <= [QuantityAfter]),
        CONSTRAINT [CK_InventoryTransactions_Import] CHECK ([Type] <> 'IMPORT' OR ([QuantityDelta] > 0 AND [Reference] IS NOT NULL AND [Reference] <> '' AND [Reason] IS NOT NULL AND [Reason] <> '')),
        CONSTRAINT [CK_InventoryTransactions_Type] CHECK ([Type] IN ('IMPORT','ORDER_PLACED','ORDER_DELIVERED','ORDER_CANCELLED')),
        CONSTRAINT [FK_InventoryTransactions_AspNetUsers_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryTransactions_Inventories_InventoryId] FOREIGN KEY ([InventoryId]) REFERENCES [Inventories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryTransactions_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUsers_LockedByUserId] ON [AspNetUsers] ([LockedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Books_CategoryId] ON [Books] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Books_IsDeleted_CreatedAt] ON [Books] ([IsDeleted], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Books_SKU] ON [Books] ([SKU]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CartItems_BookId] ON [CartItems] ([BookId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CartItems_CartId_BookId] ON [CartItems] ([CartId], [BookId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Carts_UserId] ON [Carts] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Categories_Name] ON [Categories] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Inventories_BookId] ON [Inventories] ([BookId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InventoryTransactions_ActorUserId] ON [InventoryTransactions] ([ActorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InventoryTransactions_InventoryId_CreatedAt] ON [InventoryTransactions] ([InventoryId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InventoryTransactions_OrderId] ON [InventoryTransactions] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderEvents_ActorUserId] ON [OrderEvents] ([ActorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderEvents_OrderId_CreatedAt] ON [OrderEvents] ([OrderId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderItems_BookId] ON [OrderItems] ([BookId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OrderItems_OrderId_BookId] ON [OrderItems] ([OrderId], [BookId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Orders_OrderCode] ON [Orders] ([OrderCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Orders_Status_DeliveredAt] ON [Orders] ([Status], [DeliveredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Orders_UserId_CreatedAt] ON [Orders] ([UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_BookId_IsHidden_CreatedAt] ON [Reviews] ([BookId], [IsHidden], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_ModeratedByUserId] ON [Reviews] ([ModeratedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Reviews_UserId_BookId] ON [Reviews] ([UserId], [BookId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SystemSettings_SettingKey] ON [SystemSettings] ([SettingKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026071626_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261026071626_InitialCreate', N'10.0.14');
END;

COMMIT;
GO

