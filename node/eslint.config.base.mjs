import eslintJs from '@eslint/js';
import tsParser from '@typescript-eslint/parser';
import importPlugin from 'eslint-plugin-import';
import globals from 'globals';
import eslintTs from 'typescript-eslint';

const tsFiles = ['**/*.ts'];

const languageOptions = {
  globals: {
    ...globals.node,
    ...globals.jest,
  },
  ecmaVersion: 2023,
  sourceType: 'module',
};

const customTypescriptConfig = {
  files: tsFiles,
  plugins: {
    import: importPlugin,
    'import/parsers': tsParser,
  },
  languageOptions: {
    ...languageOptions,
    parser: tsParser,
    parserOptions: {
      project: './tsconfig.json',
    },
  },
  settings: {
    'import/parsers': {
      '@typescript-eslint/parser': ['.ts'],
    },
  },
  rules: {
    'import/export': 'error',
    'import/no-duplicates': 'warn',
    ...importPlugin.configs.typescript.rules,
    '@typescript-eslint/no-use-before-define': 'off',
    'require-await': 'off',
    'no-duplicate-imports': 'error',
    'no-unneeded-ternary': 'error',
    'prefer-object-spread': 'error',

    '@typescript-eslint/no-unused-vars': [
      'error',
      {
        ignoreRestSiblings: true,
        args: 'none',
      },
    ],
  },
};

// Add the files for applying the recommended TypeScript configs
// only for the Typescript files.
// This is necessary when we have the multiple extensions files
// (e.g. .ts, .tsx, .js, .cjs, .mjs, etc.).
const recommendedTypeScriptConfigs = [
  ...eslintTs.configs.recommended.map((config) => ({
    ...config,
    files: tsFiles,
  })),
  ...eslintTs.configs.stylistic.map((config) => ({
    ...config,
    files: tsFiles,
  })),
];

export default [
  { ignores: ['docs/*', 'build/*', 'lib/*', 'dist/*'] }, // global ignores
  eslintJs.configs.recommended,
  ...recommendedTypeScriptConfigs,
  customTypescriptConfig,
];
