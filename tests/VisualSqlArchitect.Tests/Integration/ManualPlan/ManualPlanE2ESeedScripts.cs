namespace DBWeaver.Tests.Integration.ManualPlan;

internal static class ManualPlanE2ESeedScripts
{
    public const string MySqlSetup = @"
CREATE DATABASE IF NOT EXISTS public;
USE public;

CREATE TABLE IF NOT EXISTS customers (
  id INT PRIMARY KEY,
  name VARCHAR(128) NOT NULL,
  email VARCHAR(255) NOT NULL,
  city VARCHAR(128) NULL
);

CREATE TABLE IF NOT EXISTS orders (
  id INT PRIMARY KEY,
  customer_id INT NOT NULL,
  status VARCHAR(32) NOT NULL,
  total DECIMAL(12,2) NOT NULL,
  created_at DATETIME NOT NULL,
  FOREIGN KEY (customer_id) REFERENCES customers(id)
);

DELETE FROM orders;
DELETE FROM customers;

INSERT INTO customers (id, name, email, city) VALUES
  (1, 'Ana Costa', 'ana@email.com', 'Sao Paulo'),
  (2, 'Bruno Lima', 'bruno@email.com', 'Rio de Janeiro'),
  (3, 'Carla Dias', 'carla@email.com', 'Belo Horizonte');

INSERT INTO orders (id, customer_id, status, total, created_at) VALUES
  (1,1,'delivered',100.00,'2024-01-01 10:00:00'),
  (2,1,'delivered',110.00,'2024-01-02 10:00:00'),
  (3,1,'paid',120.00,'2024-01-03 10:00:00'),
  (4,2,'delivered',130.00,'2024-01-04 10:00:00'),
  (5,2,'delivered',140.00,'2024-01-05 10:00:00'),
  (6,2,'shipped',150.00,'2024-01-06 10:00:00'),
  (7,3,'delivered',160.00,'2024-01-07 10:00:00'),
  (8,3,'cancelled',170.00,'2024-01-08 10:00:00'),
  (9,3,'pending',180.00,'2024-01-09 10:00:00'),
  (10,1,'delivered',190.00,'2024-01-10 10:00:00'),
  (11,2,'delivered',200.00,'2024-01-11 10:00:00'),
  (12,3,'delivered',210.00,'2024-01-12 10:00:00'),
  (13,1,'refunded',220.00,'2024-01-13 10:00:00'),
  (14,2,'delivered',230.00,'2024-01-14 10:00:00');
";

    public const string PostgresSetup = @"
CREATE SCHEMA IF NOT EXISTS public;

CREATE TABLE IF NOT EXISTS public.customers (
  id INT PRIMARY KEY,
  name TEXT NOT NULL,
  email TEXT NOT NULL,
  city TEXT NULL
);

CREATE TABLE IF NOT EXISTS public.orders (
  id INT PRIMARY KEY,
  customer_id INT NOT NULL REFERENCES public.customers(id),
  status TEXT NOT NULL,
  total NUMERIC(12,2) NOT NULL,
  created_at TIMESTAMP NOT NULL
);

TRUNCATE TABLE public.orders, public.customers RESTART IDENTITY CASCADE;

INSERT INTO public.customers (id, name, email, city) VALUES
  (1, 'Ana Costa', 'ana@email.com', 'Sao Paulo'),
  (2, 'Bruno Lima', 'bruno@email.com', 'Rio de Janeiro'),
  (3, 'Carla Dias', 'carla@email.com', 'Belo Horizonte');

INSERT INTO public.orders (id, customer_id, status, total, created_at) VALUES
  (1,1,'delivered',100.00,'2024-01-01 10:00:00'),
  (2,1,'delivered',110.00,'2024-01-02 10:00:00'),
  (3,1,'paid',120.00,'2024-01-03 10:00:00'),
  (4,2,'delivered',130.00,'2024-01-04 10:00:00'),
  (5,2,'delivered',140.00,'2024-01-05 10:00:00'),
  (6,2,'shipped',150.00,'2024-01-06 10:00:00'),
  (7,3,'delivered',160.00,'2024-01-07 10:00:00'),
  (8,3,'cancelled',170.00,'2024-01-08 10:00:00'),
  (9,3,'pending',180.00,'2024-01-09 10:00:00'),
  (10,1,'delivered',190.00,'2024-01-10 10:00:00'),
  (11,2,'delivered',200.00,'2024-01-11 10:00:00'),
  (12,3,'delivered',210.00,'2024-01-12 10:00:00'),
  (13,1,'refunded',220.00,'2024-01-13 10:00:00'),
  (14,2,'delivered',230.00,'2024-01-14 10:00:00');
";

    public const string SqlServerSetup = @"
IF OBJECT_ID('dbo.customers', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[customers] (
      [id] INT NOT NULL PRIMARY KEY,
      [name] NVARCHAR(128) NOT NULL,
      [email] NVARCHAR(255) NOT NULL,
      [city] NVARCHAR(128) NULL
    );
END;

IF OBJECT_ID('dbo.orders', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[orders] (
      [id] INT NOT NULL PRIMARY KEY,
      [customer_id] INT NOT NULL,
      [status] NVARCHAR(32) NOT NULL,
      [total] DECIMAL(12,2) NOT NULL,
      [created_at] DATETIME2 NOT NULL,
      CONSTRAINT FK_orders_customers FOREIGN KEY ([customer_id]) REFERENCES [dbo].[customers]([id])
    );
END;

DELETE FROM [dbo].[orders];
DELETE FROM [dbo].[customers];

INSERT INTO [dbo].[customers] ([id], [name], [email], [city]) VALUES
  (1, N'Ana Costa', N'ana@email.com', N'Sao Paulo'),
  (2, N'Bruno Lima', N'bruno@email.com', N'Rio de Janeiro'),
  (3, N'Carla Dias', N'carla@email.com', N'Belo Horizonte');

INSERT INTO [dbo].[orders] ([id], [customer_id], [status], [total], [created_at]) VALUES
  (1,1,N'delivered',100.00,'2024-01-01T10:00:00'),
  (2,1,N'delivered',110.00,'2024-01-02T10:00:00'),
  (3,1,N'paid',120.00,'2024-01-03T10:00:00'),
  (4,2,N'delivered',130.00,'2024-01-04T10:00:00'),
  (5,2,N'delivered',140.00,'2024-01-05T10:00:00'),
  (6,2,N'shipped',150.00,'2024-01-06T10:00:00'),
  (7,3,N'delivered',160.00,'2024-01-07T10:00:00'),
  (8,3,N'cancelled',170.00,'2024-01-08T10:00:00'),
  (9,3,N'pending',180.00,'2024-01-09T10:00:00'),
  (10,1,N'delivered',190.00,'2024-01-10T10:00:00'),
  (11,2,N'delivered',200.00,'2024-01-11T10:00:00'),
  (12,3,N'delivered',210.00,'2024-01-12T10:00:00'),
  (13,1,N'refunded',220.00,'2024-01-13T10:00:00'),
  (14,2,N'delivered',230.00,'2024-01-14T10:00:00');
";
}
