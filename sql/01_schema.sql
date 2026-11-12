USE RealEstateDb;
GO

CREATE TABLE Roles (
    RoleId  int IDENTITY PRIMARY KEY,
    Name    nvarchar(50) NOT NULL CONSTRAINT UQ_Roles_Name UNIQUE
);

CREATE TABLE Users (
    UserId    int IDENTITY PRIMARY KEY,
    RoleId    int NOT NULL REFERENCES Roles(RoleId),
    Email     nvarchar(255) NOT NULL,
    FullName  nvarchar(150) NOT NULL,
    Phone     nvarchar(20) NULL,
    IsActive  bit NOT NULL DEFAULT 1,
    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE Agents (
    AgentId        int IDENTITY PRIMARY KEY,
    UserId         int NOT NULL CONSTRAINT UQ_Agents_User UNIQUE REFERENCES Users(UserId),
    LicenseNo      nvarchar(30) NOT NULL,
    CommissionRate decimal(5,4) NOT NULL CONSTRAINT CK_Agents_Rate CHECK (CommissionRate BETWEEN 0 AND 0.2),
    HireDate       date NOT NULL,
    IsDeleted      bit NOT NULL DEFAULT 0,
    DeletedAt      datetime2 NULL,
    DeletedBy      int NULL REFERENCES Users(UserId)
);

CREATE TABLE Owners (
    OwnerId   int IDENTITY PRIMARY KEY,
    FullName  nvarchar(150) NOT NULL,
    Phone     nvarchar(20) NOT NULL,
    Email     nvarchar(255) NULL,
    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL DEFAULT 0,
    DeletedAt datetime2 NULL,
    DeletedBy int NULL REFERENCES Users(UserId)
);

CREATE TABLE Clients (
    ClientId   int IDENTITY PRIMARY KEY,
    FullName   nvarchar(150) NOT NULL,
    Phone      nvarchar(20) NOT NULL,
    Email      nvarchar(255) NULL,
    ClientType nvarchar(10) NOT NULL CONSTRAINT CK_Clients_Type CHECK (ClientType IN (N'Buyer', N'Tenant')),
    CreatedAt  datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy  int NOT NULL REFERENCES Users(UserId),
    UpdatedAt  datetime2 NULL,
    UpdatedBy  int NULL REFERENCES Users(UserId),
    IsDeleted  bit NOT NULL DEFAULT 0,
    DeletedAt  datetime2 NULL,
    DeletedBy  int NULL REFERENCES Users(UserId)
);

CREATE TABLE Cities (
    CityId int IDENTITY PRIMARY KEY,
    Name   nvarchar(100) NOT NULL CONSTRAINT UQ_Cities_Name UNIQUE,
    Region nvarchar(100) NOT NULL
);

CREATE TABLE Districts (
    DistrictId int IDENTITY PRIMARY KEY,
    CityId     int NOT NULL REFERENCES Cities(CityId),
    Name       nvarchar(100) NOT NULL,
    CONSTRAINT UQ_Districts_City_Name UNIQUE (CityId, Name)
);

CREATE TABLE Addresses (
    AddressId  int IDENTITY PRIMARY KEY,
    DistrictId int NOT NULL REFERENCES Districts(DistrictId),
    Street     nvarchar(150) NOT NULL,
    Building   nvarchar(10) NOT NULL,
    Apartment  nvarchar(10) NULL,
    PostalCode char(5) NULL
);

CREATE TABLE PropertyTypes (
    PropertyTypeId int IDENTITY PRIMARY KEY,
    Name           nvarchar(50) NOT NULL CONSTRAINT UQ_PropertyTypes_Name UNIQUE
);

CREATE TABLE Properties (
    PropertyId     int IDENTITY PRIMARY KEY,
    OwnerId        int NOT NULL REFERENCES Owners(OwnerId),
    PropertyTypeId int NOT NULL REFERENCES PropertyTypes(PropertyTypeId),
    AddressId      int NOT NULL REFERENCES Addresses(AddressId),
    Title          nvarchar(200) NOT NULL,
    Description    nvarchar(max) NULL,
    AreaSqm        decimal(9,2) NOT NULL CONSTRAINT CK_Properties_Area CHECK (AreaSqm > 0),
    Rooms          tinyint NOT NULL,
    Floor          smallint NULL,
    YearBuilt      smallint NULL CONSTRAINT CK_Properties_Year CHECK (YearBuilt BETWEEN 1800 AND 2100),
    CreatedAt      datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy      int NOT NULL REFERENCES Users(UserId),
    UpdatedAt      datetime2 NULL,
    UpdatedBy      int NULL REFERENCES Users(UserId),
    IsDeleted      bit NOT NULL DEFAULT 0,
    DeletedAt      datetime2 NULL,
    DeletedBy      int NULL REFERENCES Users(UserId)
);

CREATE TABLE Amenities (
    AmenityId int IDENTITY PRIMARY KEY,
    Name      nvarchar(50) NOT NULL CONSTRAINT UQ_Amenities_Name UNIQUE
);

CREATE TABLE PropertyAmenities (
    PropertyId int NOT NULL REFERENCES Properties(PropertyId),
    AmenityId  int NOT NULL REFERENCES Amenities(AmenityId),
    CONSTRAINT PK_PropertyAmenities PRIMARY KEY (PropertyId, AmenityId)
);

CREATE TABLE PropertyPhotos (
    PhotoId    int IDENTITY PRIMARY KEY,
    PropertyId int NOT NULL REFERENCES Properties(PropertyId),
    Url        nvarchar(500) NOT NULL,
    IsMain     bit NOT NULL DEFAULT 0,
    SortOrder  int NOT NULL DEFAULT 0
);

CREATE TABLE Listings (
    ListingId   int IDENTITY PRIMARY KEY,
    PropertyId  int NOT NULL REFERENCES Properties(PropertyId),
    AgentId     int NOT NULL REFERENCES Agents(AgentId),
    ListingType nvarchar(10) NOT NULL CONSTRAINT CK_Listings_Type CHECK (ListingType IN (N'Sale', N'Rent')),
    Price       decimal(14,2) NOT NULL CONSTRAINT CK_Listings_Price CHECK (Price > 0),
    Currency    char(3) NOT NULL DEFAULT 'UAH',
    Status      nvarchar(10) NOT NULL DEFAULT N'Active'
                CONSTRAINT CK_Listings_Status CHECK (Status IN (N'Active', N'Reserved', N'Closed')),
    ListedAt    datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    RowVersion  rowversion,
    CreatedAt   datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy   int NOT NULL REFERENCES Users(UserId),
    UpdatedAt   datetime2 NULL,
    UpdatedBy   int NULL REFERENCES Users(UserId),
    IsDeleted   bit NOT NULL DEFAULT 0,
    DeletedAt   datetime2 NULL,
    DeletedBy   int NULL REFERENCES Users(UserId)
);

CREATE TABLE PriceHistory (
    PriceHistoryId int IDENTITY PRIMARY KEY,
    ListingId      int NOT NULL REFERENCES Listings(ListingId),
    OldPrice       decimal(14,2) NOT NULL,
    NewPrice       decimal(14,2) NOT NULL,
    ChangedAt      datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ChangedBy      int NULL REFERENCES Users(UserId)
);

CREATE TABLE Viewings (
    ViewingId   int IDENTITY PRIMARY KEY,
    ListingId   int NOT NULL REFERENCES Listings(ListingId),
    ClientId    int NOT NULL REFERENCES Clients(ClientId),
    AgentId     int NOT NULL REFERENCES Agents(AgentId),
    ScheduledAt datetime2 NOT NULL,
    Status      nvarchar(10) NOT NULL DEFAULT N'Scheduled'
                CONSTRAINT CK_Viewings_Status CHECK (Status IN (N'Scheduled', N'Done', N'Cancelled')),
    Feedback    nvarchar(500) NULL
);

CREATE TABLE Contracts (
    ContractId       int IDENTITY PRIMARY KEY,
    ListingId        int NOT NULL CONSTRAINT UQ_Contracts_Listing UNIQUE REFERENCES Listings(ListingId),
    ClientId         int NOT NULL REFERENCES Clients(ClientId),
    AgentId          int NOT NULL REFERENCES Agents(AgentId),
    ContractType     nvarchar(10) NOT NULL CONSTRAINT CK_Contracts_Type CHECK (ContractType IN (N'Sale', N'Lease')),
    TotalAmount      decimal(14,2) NOT NULL CONSTRAINT CK_Contracts_Total CHECK (TotalAmount > 0),
    CommissionAmount decimal(14,2) NOT NULL,
    StartDate        date NOT NULL,
    EndDate          date NULL,
    Status           nvarchar(12) NOT NULL DEFAULT N'Active'
                     CONSTRAINT CK_Contracts_Status CHECK (Status IN (N'Active', N'Completed', N'Terminated')),
    SignedAt         datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedAt        datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy        int NOT NULL REFERENCES Users(UserId),
    UpdatedAt        datetime2 NULL,
    UpdatedBy        int NULL REFERENCES Users(UserId),
    CONSTRAINT CK_Contracts_Dates CHECK (EndDate IS NULL OR EndDate >= StartDate)
);

CREATE TABLE Payments (
    PaymentId  int IDENTITY PRIMARY KEY,
    ContractId int NOT NULL REFERENCES Contracts(ContractId),
    Amount     decimal(14,2) NOT NULL CONSTRAINT CK_Payments_Amount CHECK (Amount > 0),
    DueDate    date NOT NULL,
    PaidAt     datetime2 NULL,
    Method     nvarchar(10) NOT NULL CONSTRAINT CK_Payments_Method CHECK (Method IN (N'Cash', N'Card', N'Transfer')),
    CreatedAt  datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy  int NOT NULL REFERENCES Users(UserId),
    UpdatedAt  datetime2 NULL,
    UpdatedBy  int NULL REFERENCES Users(UserId)
);

CREATE TABLE MaintenanceRequests (
    RequestId   int IDENTITY PRIMARY KEY,
    PropertyId  int NOT NULL REFERENCES Properties(PropertyId),
    ClientId    int NULL REFERENCES Clients(ClientId),
    Description nvarchar(1000) NOT NULL,
    Priority    nvarchar(10) NOT NULL CONSTRAINT CK_Maintenance_Priority CHECK (Priority IN (N'Low', N'Medium', N'High')),
    Status      nvarchar(12) NOT NULL DEFAULT N'Open'
                CONSTRAINT CK_Maintenance_Status CHECK (Status IN (N'Open', N'InProgress', N'Done')),
    CreatedAt   datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ClosedAt    datetime2 NULL
);

CREATE TABLE Reviews (
    ReviewId  int IDENTITY PRIMARY KEY,
    ClientId  int NOT NULL REFERENCES Clients(ClientId),
    AgentId   int NOT NULL REFERENCES Agents(AgentId),
    Rating    tinyint NOT NULL CONSTRAINT CK_Reviews_Rating CHECK (Rating BETWEEN 1 AND 5),
    Comment   nvarchar(1000) NULL,
    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
