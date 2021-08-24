
-- ----------------------------
-- Table structure for CITY
-- ----------------------------
DROP TABLE "YZN"."CITY";
CREATE TABLE "YZN"."CITY" (
"ID" NUMBER NOT NULL ,
"NAME" NVARCHAR2(255) NULL ,
"PROVINCEID" NUMBER NULL 
)
LOGGING
NOCOMPRESS
NOCACHE

;

-- ----------------------------
-- Indexes structure for table CITY
-- ----------------------------

-- ----------------------------
-- Checks structure for table CITY
-- ----------------------------
ALTER TABLE "YZN"."CITY" ADD CHECK ("ID" IS NOT NULL);

-- ----------------------------
-- Primary Key structure for table CITY
-- ----------------------------
ALTER TABLE "YZN"."CITY" ADD PRIMARY KEY ("ID");



-- ----------------------------
-- Table structure for USERS
-- ----------------------------
DROP TABLE "YZN"."USERS";
CREATE TABLE "YZN"."USERS" (
"ID" NUMBER NOT NULL ,
"NAME" NVARCHAR2(255) NULL ,
"AGE" NUMBER NULL ,
"CITYID" NUMBER NULL ,
"OPTIME" DATE NULL ,
"GENDER" NUMBER NULL 
)
LOGGING
NOCOMPRESS
NOCACHE

;


ALTER TABLE "YZN"."USERS" ADD CHECK ("ID" IS NOT NULL);


ALTER TABLE "YZN"."USERS" ADD PRIMARY KEY ("ID");

-- ----------------------------
-- Sequence structure for USERS_AUTOID
-- ----------------------------
DROP SEQUENCE "YZN"."USERS_AUTOID";
CREATE SEQUENCE "YZN"."USERS_AUTOID"
 INCREMENT BY 1
 MINVALUE 1
 MAXVALUE 9999999999999999999999999999
 START WITH 1
 CACHE 20;
 
create or replace trigger users_id
before insert on users  --before:执行DML等操作之前触发
for each row  --行级触发器
begin 
	select USERS_AUTOID.nextval into :new.id from dual;
end;

-- ----------------------------
-- Table structure for PROVINCE
-- ----------------------------
DROP TABLE "YZN"."PROVINCE";
CREATE TABLE "YZN"."PROVINCE" (
"ID" NUMBER NOT NULL ,
"NAME" NVARCHAR2(255) NULL 
)
LOGGING
NOCOMPRESS
NOCACHE

ALTER TABLE "YZN"."PROVINCE" ADD CHECK ("ID" IS NOT NULL);

ALTER TABLE "YZN"."PROVINCE" ADD PRIMARY KEY ("ID");


CREATE OR REPLACE 
function MyFunction(id INTEGER)
 return VARCHAR
as
begin
   return 'id: ' || "TO_CHAR"(id);
end;
