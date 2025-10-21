# 数据库管理脚本说明

本目录包含用于管理 RealScene3D 项目数据库的实用脚本。

## 脚本列表

### 1. check-migration-sync（检查迁移同步）

**文件**: `check-migration-sync.bat` (Windows) / `check-migration-sync.sh` (Linux/Mac)

**用途**: 检测实体模型与数据库迁移是否同步，如果发现不同步会提示创建新迁移。

**使用场景**:
- 修改了实体类（Domain/Entities）后，忘记创建迁移
- 团队协作时，拉取代码后检查是否有未同步的模型变更
- 部署前验证数据库迁移是否完整

**使用方法**:

Windows:
```bash
# 方式1: 双击运行
双击 scripts/check-migration-sync.bat

# 方式2: 命令行运行
cd scripts
check-migration-sync.bat
```

Linux/Mac:
```bash
# 从项目根目录运行
./scripts/check-migration-sync.sh

# 或者进入 scripts 目录
cd scripts
./check-migration-sync.sh
```

**执行流程**:
1. 构建项目以确保代码无误
2. 尝试创建临时迁移来检测模型变更
3. 如果检测到变更：
   - 提示用户输入迁移名称
   - 创建新的迁移文件
   - 询问是否立即应用到数据库
4. 如果没有检测到变更：
   - 显示 "所有实体模型已同步" 消息并退出

**示例输出**:

同步情况:
```
========================================
Checking Entity-Migration Sync...
========================================

Step 1: Building the project...
Step 2: Checking for pending model changes...

[OK] All entity models are in sync with migrations!
No action needed.
```

不同步情况:
```
========================================
Checking Entity-Migration Sync...
========================================

Step 1: Building the project...
Step 2: Checking for pending model changes...

[WARNING] Entity models are NOT in sync with database!

Changes detected in entity models that are not reflected in migrations.

Would you like to:
1. Create a new migration to sync the changes
2. Exit without making changes

Enter your choice (1 or 2): 1

Enter migration name (e.g., SyncEntityChanges): AddMissingFields

Creating migration: AddMissingFields...
Done. To undo this action, use 'ef migrations remove'

Migration created successfully!

Would you like to apply this migration to the database now? (Y/N)
Your choice: Y

Applying migration to database...
Done.

========================================
Migration applied successfully!
========================================
```

---

### 2. reset-database（重置数据库）

**文件**: `reset-database.bat` (Windows) / `reset-database.sh` (Linux/Mac)

**用途**: 删除现有数据库并重新应用所有迁移，用于开发环境重置。

**警告**: ⚠️ 此操作会删除所有数据！仅在开发环境使用！

**使用场景**:
- 开发环境数据库损坏需要重建
- 迁移文件冲突，需要从头开始
- 测试完整的迁移流程

**使用方法**:

Windows:
```bash
# 方式1: 双击运行
双击 scripts/reset-database.bat

# 方式2: 命令行运行
cd scripts
reset-database.bat
```

Linux/Mac:
```bash
# 从项目根目录运行
./scripts/reset-database.sh

# 或者进入 scripts 目录
cd scripts
./reset-database.sh
```

**执行流程**:
1. 删除现有数据库（使用 --force 参数）
2. 重新创建数据库并应用所有迁移
3. 显示执行结果

---

## 最佳实践

### 开发工作流

1. **修改实体类后**:
   ```bash
   # 运行同步检查脚本
   ./scripts/check-migration-sync.sh

   # 按提示创建并应用迁移
   ```

2. **拉取团队代码后**:
   ```bash
   # 检查是否有新的迁移需要应用
   cd src/RealScene3D.Infrastructure
   dotnet ef database update --context ApplicationDbContext --startup-project ../RealScene3D.WebApi
   ```

3. **遇到迁移冲突时**:
   ```bash
   # 重置数据库（仅开发环境）
   ./scripts/reset-database.sh
   ```

### 常见问题

**Q: 脚本报错 "Build failed"**
- A: 请先解决项目编译错误，确保代码可以正常构建

**Q: 检测到多个 DbContext**
- A: 脚本默认使用 `ApplicationDbContext`，如需使用其他 Context 请修改脚本中的 `--context` 参数

**Q: 迁移创建成功但应用失败**
- A: 检查数据库连接字符串配置，确保数据库服务正在运行

**Q: Linux/Mac 下脚本无法执行**
- A: 运行 `chmod +x scripts/*.sh` 添加执行权限

---

## 手动操作命令

如果需要手动执行迁移操作，可以使用以下命令：

```bash
# 进入 Infrastructure 项目目录
cd src/RealScene3D.Infrastructure

# 创建新迁移
dotnet ef migrations add MigrationName --context ApplicationDbContext --startup-project ../RealScene3D.WebApi

# 应用迁移到数据库
dotnet ef database update --context ApplicationDbContext --startup-project ../RealScene3D.WebApi

# 回滚到指定迁移
dotnet ef database update PreviousMigrationName --context ApplicationDbContext --startup-project ../RealScene3D.WebApi

# 移除最后一个迁移（仅当未应用到数据库时）
dotnet ef migrations remove --context ApplicationDbContext --startup-project ../RealScene3D.WebApi

# 查看迁移列表
dotnet ef migrations list --context ApplicationDbContext --startup-project ../RealScene3D.WebApi

# 生成 SQL 脚本（用于生产环境）
dotnet ef migrations script --context ApplicationDbContext --startup-project ../RealScene3D.WebApi --output migration.sql
```

---

## 生产环境部署

⚠️ **重要**: 生产环境请使用 SQL 脚本方式部署，不要直接运行迁移命令！

```bash
# 1. 生成迁移 SQL 脚本
dotnet ef migrations script --context ApplicationDbContext --startup-project ../RealScene3D.WebApi --idempotent --output deploy.sql

# 2. 审查 SQL 脚本内容

# 3. 在生产数据库执行 SQL 脚本
psql -h <host> -U <user> -d <database> -f deploy.sql
```

`--idempotent` 参数会生成可重复执行的脚本，如果迁移已应用则跳过。

---

## 技术说明

- **数据库**: PostgreSQL with PostGIS extension
- **ORM**: Entity Framework Core 8.0
- **迁移工具**: dotnet ef tools
- **支持的 DbContext**: ApplicationDbContext, PostgreSqlDbContext, MongoDbContext
- **默认 Context**: ApplicationDbContext（用于关系型数据库）

---

## 相关文档

- [Entity Framework Core Migrations](https://docs.microsoft.com/ef/core/managing-schemas/migrations/)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
- [PostGIS Documentation](https://postgis.net/documentation/)
