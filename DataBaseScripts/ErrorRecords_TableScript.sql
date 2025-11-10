USE [ExcelImportDB]
GO

/****** Object:  Table [dbo].[ErrorRecords]    Script Date: 10-11-2025 17:24:49 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[ErrorRecords](
	[ErrorId] [int] IDENTITY(1,1) NOT NULL,
	[FileName] [nvarchar](255) NULL,
	[TableName] [nvarchar](100) NULL,
	[ErrorDetails] [nvarchar](max) NULL,
	[LoggedDate] [datetime] NULL,
PRIMARY KEY CLUSTERED 
(
	[ErrorId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[ErrorRecords] ADD  DEFAULT (getdate()) FOR [LoggedDate]
GO


