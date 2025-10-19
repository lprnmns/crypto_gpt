import tseslint from 'typescript-eslint'
import eslintPluginImport from 'eslint-plugin-import'

export default tseslint.config(
  tseslint.configs.recommended,
  eslintPluginImport.flatConfigs.recommended,
  {
    files: ['**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': ['error', {
        paths: [
          { name: 'express', message: 'Backend ASP.NET Core olacak.' },
          { name: 'prisma', message: 'ORM: EF Core (Npgsql) kullanılacak.' },
          { name: 'mongoose', message: 'DB: PostgreSQL kullanılacak.' },
          { name: 'typeorm', message: 'ORM: EF Core kullanılacak.' }
        ],
        patterns: [
          { group: ['fs','path','child_process'], message: 'Client-side: Node yerleşikleri yok.' }
        ]
      }]
    }
  }
)
