-- 简单直接的更新脚本
-- 将所有场景对象的位置更新为北京天安门广场坐标

-- 更新所有场景对象的位置
UPDATE "SceneObjects"
SET "Position" = ST_SetSRID(ST_MakePoint(116.397128, 39.908802, 100), 4326),
    "UpdatedAt" = NOW();

-- 查询更新结果
SELECT
    id,
    name,
    ST_X("Position") AS longitude,
    ST_Y("Position") AS latitude,
    ST_Z("Position") AS height
FROM "SceneObjects"
ORDER BY "UpdatedAt" DESC
LIMIT 10;
