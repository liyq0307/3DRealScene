/**
 * 文件系统访问 API 句柄存储
 *
 * 此服务提供了一个简单的接口来存储和检索 IndexedDB 中的 FileSystemFileHandle 对象。
 * 这允许跨浏览器会话持久化访问本地文件。
 *
 * 作者：liyq
 * 日期：2025-10-22
 * 更新：添加了类型安全、错误处理、输入验证和日志记录
 */

// 类型定义
interface FileHandleStoreOptions {
  dbName?: string;
  storeName?: string;
  dbVersion?: number;
}

interface Logger {
  info: (message: string, ...args: any[]) => void;
  warn: (message: string, ...args: any[]) => void;
  error: (message: string, ...args: any[]) => void;
}

// 自定义错误类
class FileHandleStoreError extends Error {
  constructor(message: string, public code?: string, public originalError?: any) {
    super(message);
    this.name = 'FileHandleStoreError';
  }
}

class BrowserSupportError extends FileHandleStoreError {
  constructor(feature: string) {
    super(`Browser does not support ${feature}`, 'BROWSER_NOT_SUPPORTED');
  }
}

class DatabaseError extends FileHandleStoreError {
  constructor(message: string, originalError?: any) {
    super(`Database operation failed: ${message}`, 'DATABASE_ERROR', originalError);
  }
}

class ValidationError extends FileHandleStoreError {
  constructor(message: string) {
    super(`Validation failed: ${message}`, 'VALIDATION_ERROR');
  }
}

// 简单日志器实现
class ConsoleLogger implements Logger {
  info(message: string, ...args: any[]): void {
    console.info(`[FileHandleStore] ${message}`, ...args);
  }

  warn(message: string, ...args: any[]): void {
    console.warn(`[FileHandleStore] ${message}`, ...args);
  }

  error(message: string, ...args: any[]): void {
    console.error(`[FileHandleStore] ${message}`, ...args);
  }
}

// 默认配置
const DEFAULT_CONFIG: Required<FileHandleStoreOptions> = {
  dbName: 'FileHandleDB',
  storeName: 'FileHandles',
  dbVersion: 1,
};

// 浏览器兼容性检查
function checkBrowserSupport(): void {
  if (!('indexedDB' in window)) {
    throw new BrowserSupportError('IndexedDB');
  }

  if (!('showOpenFilePicker' in window)) {
    throw new BrowserSupportError('File System Access API');
  }
}

// 输入验证
function validateKey(key: string): void {
  if (!key || typeof key !== 'string') {
    throw new ValidationError('Key must be a non-empty string');
  }

  if (key.length > 500) {
    throw new ValidationError('Key length must not exceed 500 characters');
  }
}

function validateHandle(handle: any): void {
  if (!handle) {
    throw new ValidationError('Handle cannot be null or undefined');
  }

  // 基本的FileSystemFileHandle检查
  if (typeof handle !== 'object' || !('kind' in handle) || !('name' in handle)) {
    throw new ValidationError('Invalid file handle object');
  }
}

/**
 * 文件句柄存储服务类
 * 提供类型安全的IndexedDB操作接口
 */
export class FileHandleStore {
  private db: IDBDatabase | null = null;
  private readonly config: Required<FileHandleStoreOptions>;
  private readonly logger: Logger;
  private dbPromise: Promise<IDBDatabase> | null = null;

  constructor(options: FileHandleStoreOptions = {}, logger: Logger = new ConsoleLogger()) {
    this.config = { ...DEFAULT_CONFIG, ...options };
    this.logger = logger;

    // 初始化时检查浏览器支持
    checkBrowserSupport();
    this.logger.info('FileHandleStore initialized', this.config);
  }

  /**
   * 获取数据库连接，包含连接池管理
   */
  private async getDatabase(): Promise<IDBDatabase> {
    if (this.db) {
      return this.db;
    }

    if (this.dbPromise) {
      return this.dbPromise;
    }

    this.dbPromise = this.openDatabase();

    try {
      this.db = await this.dbPromise;
      this.logger.info('Database opened successfully');
      return this.db;
    } finally {
      this.dbPromise = null;
    }
  }

  /**
   * 打开数据库并处理版本升级
   */
  private async openDatabase(): Promise<IDBDatabase> {
    return new Promise((resolve, reject) => {
      const request = indexedDB.open(this.config.dbName, this.config.dbVersion);

      request.onerror = () => {
        const error = new DatabaseError('Failed to open database', request.error);
        this.logger.error('Database open failed', error);
        reject(error);
      };

      request.onsuccess = () => {
        const db = request.result;
        this.logger.info('Database opened successfully', { name: db.name, version: db.version });
        resolve(db);
      };

      request.onupgradeneeded = (event) => {
        const db = (event.target as IDBOpenDBRequest).result;
        this.logger.info('Database upgrade needed', { oldVersion: event.oldVersion, newVersion: event.newVersion });

        // 创建对象存储
        if (!db.objectStoreNames.contains(this.config.storeName)) {
          db.createObjectStore(this.config.storeName);
          this.logger.info('Created object store', { storeName: this.config.storeName });
        }

        // 这里可以添加未来的版本升级逻辑
      };

      request.onblocked = () => {
        this.logger.warn('Database open blocked - another connection is open');
      };
    });
  }

  /**
   * 保存文件句柄到数据库
   */
  async saveHandle(key: string, handle: FileSystemFileHandle): Promise<void> {
    try {
      validateKey(key);
      validateHandle(handle);

      const db = await this.getDatabase();
      await this.performTransaction(db, 'readwrite', async (store) => {
        const request = store.put(handle, key);
        await new Promise<void>((resolve, reject) => {
          request.onsuccess = () => resolve();
          request.onerror = () => reject(request.error);
        });
        this.logger.info('Handle saved successfully', { key });
      });
    } catch (error) {
      this.logger.error('Failed to save handle', { key, error });
      throw error instanceof FileHandleStoreError ? error : new DatabaseError('Save operation failed', error);
    }
  }

  /**
   * 从数据库获取文件句柄
   */
  async getHandle<T extends FileSystemFileHandle = FileSystemFileHandle>(key: string): Promise<T | undefined> {
    try {
      validateKey(key);

      const db = await this.getDatabase();
      let result: T | undefined;

      await this.performTransaction(db, 'readonly', async (store) => {
        const request = store.get(key);
        result = await new Promise<T | undefined>((resolve, reject) => {
          request.onsuccess = () => resolve(request.result as T | undefined);
          request.onerror = () => reject(request.error);
        });
      });

      if (result) {
        this.logger.info('Handle retrieved successfully', { key });
      } else {
        this.logger.warn('Handle not found', { key });
      }

      return result;
    } catch (error) {
      this.logger.error('Failed to get handle', { key, error });
      throw error instanceof FileHandleStoreError ? error : new DatabaseError('Get operation failed', error);
    }
  }

  /**
   * 从数据库删除文件句柄
   */
  async deleteHandle(key: string): Promise<void> {
    try {
      validateKey(key);

      const db = await this.getDatabase();
      await this.performTransaction(db, 'readwrite', async (store) => {
        const request = store.delete(key);
        await new Promise<void>((resolve, reject) => {
          request.onsuccess = () => resolve();
          request.onerror = () => reject(request.error);
        });
        this.logger.info('Handle deleted successfully', { key });
      });
    } catch (error) {
      this.logger.error('Failed to delete handle', { key, error });
      throw error instanceof FileHandleStoreError ? error : new DatabaseError('Delete operation failed', error);
    }
  }

  /**
   * 获取所有存储的句柄键
   */
  async getAllKeys(): Promise<string[]> {
    try {
      const db = await this.getDatabase();
      let keys: string[] = [];

      await this.performTransaction(db, 'readonly', async (store) => {
        const request = store.getAllKeys();
        await new Promise<void>((resolve, reject) => {
          request.onsuccess = () => {
            keys = Array.from(request.result as string[]);
            resolve();
          };
          request.onerror = () => reject(request.error);
        });
      });

      this.logger.info('Retrieved all keys', { count: keys.length });
      return keys;
    } catch (error) {
      this.logger.error('Failed to get all keys', { error });
      throw error instanceof FileHandleStoreError ? error : new DatabaseError('Get all keys operation failed', error);
    }
  }

  /**
   * 获取句柄及其路径信息的工具方法
   * 返回包含句柄和路径的对象
   */
  async getHandleWithPath(key: string): Promise<{ handle: FileSystemFileHandle; path: string | null } | undefined> {
    try {
      const handle = await this.getHandle(key);
      if (!handle) {
        return undefined;
      }

      const path = await this.getFilePath(handle);
      return { handle, path };
    } catch (error) {
      this.logger.error('Failed to get handle with path', { key, error });
      return undefined;
    }
  }

  /**
   * 获取文件句柄对应的文件路径
   * 注意：File System Access API 目前不支持直接获取文件的完整路径
   * 此方法尝试通过多种方式获取路径信息
   */
  async getFilePath(handle: FileSystemFileHandle): Promise<string | null> {
    try {
      // 方法1：尝试获取webkitRelativePath（Safari支持）
      if ('webkitRelativePath' in handle && (handle as any).webkitRelativePath) {
        return (handle as any).webkitRelativePath as string;
      }

      // 方法2：检查是否为FileSystemFileHandle的扩展属性
      const handleAny = handle as any;
      if (handleAny.path) {
        return handleAny.path;
      }

      // 方法3：尝试通过getFile()获取文件对象，然后检查webkitRelativePath
      try {
        const file = await handle.getFile();
        if ((file as any).webkitRelativePath) {
          return (file as any).webkitRelativePath as string;
        }
      } catch (fileError) {
        this.logger.warn('Failed to get file for path extraction', { error: fileError });
      }

      // 方法4：检查handle的原型链和属性
      const proto = Object.getPrototypeOf(handle);
      if (proto && proto.constructor && proto.constructor.name) {
        this.logger.info('Handle prototype info', {
          name: proto.constructor.name,
          properties: Object.getOwnPropertyNames(proto)
        });
      }

      // 如果都没有找到路径，返回null
      this.logger.warn('Unable to retrieve file path from handle', {
        name: handle.name,
        kind: handle.kind
      });
      return null;

    } catch (error) {
      this.logger.error('Error while trying to get file path', { error });
      return null;
    }
  }

  /**
   * 清理资源
   */
  async close(): Promise<void> {
    if (this.db) {
      this.db.close();
      this.db = null;
      this.logger.info('Database connection closed');
    }
  }

  /**
   * 执行事务的通用方法
   */
  private async performTransaction<T>(
    db: IDBDatabase,
    mode: IDBTransactionMode,
    operation: (store: IDBObjectStore) => Promise<T>
  ): Promise<T> {
    return new Promise((resolve, reject) => {
      const transaction = db.transaction(this.config.storeName, mode);
      const store = transaction.objectStore(this.config.storeName);

      transaction.oncomplete = () => {
        this.logger.info('Transaction completed successfully');
      };

      transaction.onerror = () => {
        const error = new DatabaseError('Transaction failed', transaction.error);
        this.logger.error('Transaction failed', error);
        reject(error);
      };

      transaction.onabort = () => {
        this.logger.warn('Transaction aborted');
      };

      // 执行操作
      operation(store).then(resolve).catch(reject);
    });
  }
}

// 导出默认实例
export default new FileHandleStore();
