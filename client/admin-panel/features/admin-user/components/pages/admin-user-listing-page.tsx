'use client';

import { useMemo, useState, useTransition } from 'react';
import Link from 'next/link';
import { useQueryStates } from 'nuqs';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Plus, Search, X } from 'lucide-react';
import { PageHeader } from '@/components/layout/page-header';
import { ListPageCard } from '@/components/layout/list-page-card';
import { messages } from '@/lib/messages.ar';
import { cn } from '@/lib/utils';
import { AdminUserTable } from '../blocks/admin-user-tables';
import { createAdminUserColumns } from '../blocks/admin-user-tables/columns';
import { TemporaryPasswordDialog } from '../blocks/temporary-password-dialog';
import { useAdminUsers } from '../../hooks/useAdminUser';
import { adminUserListingParsers, normalizeAdminUserPageSize } from '../../search-params';
import type {
  AdminUserQueryParams,
  AdminUserRole,
  TemporaryCredential,
} from '../../types';

const m = messages.adminUser.listing;
const roleLabels = messages.adminUser.role;
const statusLabels = messages.adminUser.status;

export function AdminUserListingPage() {
  const [, startTransition] = useTransition();
  const [credential, setCredential] = useState<TemporaryCredential | null>(null);

  const [{ page, limit, search, role, status }, setParams] = useQueryStates(
    adminUserListingParsers,
    {
      history: 'replace',
      shallow: true,
      startTransition,
    },
  );

  function handleSearchChange(value: string) {
    void setParams({ search: value || null, page: null }, { throttleMs: 300 });
  }

  function handlePageChange(newPage: number) {
    void setParams({ page: newPage });
  }

  function handlePageSizeChange(newLimit: number) {
    void setParams({ limit: newLimit, page: null });
  }

  const pageSize = normalizeAdminUserPageSize(limit);

  const queryParams: AdminUserQueryParams = {
    page,
    limit: pageSize,
    ...(search && { search }),
    ...(role && { role: role as AdminUserRole }),
    ...(status && { status }),
    sortBy: 'createdAt',
    sortOrder: 'desc',
  };

  const { data: listData, isLoading: isListLoading } = useAdminUsers(queryParams);

  const columns = useMemo(() => createAdminUserColumns(setCredential), []);

  const showEmpty =
    !isListLoading && !search && !role && !status && (listData?.items.length ?? 0) === 0;

  return (
    <div className="flex flex-1 flex-col gap-4">
      <PageHeader
        title={m.title}
        description={m.description}
        action={
          <Button id="admin-users-add-btn" asChild>
            <Link href="/admin-users/new">
              <Plus className="size-4" />
              {m.addButton}
            </Link>
          </Button>
        }
      />

      <ListPageCard>
        <ListPageCard.Toolbar>
          <div className="flex flex-1 flex-wrap items-center gap-3">
            <div className="relative max-w-sm flex-1">
              <Search className="text-muted-foreground pointer-events-none absolute top-1/2 start-3 size-4 -translate-y-1/2" />
              <Input
                id="admin-users-search-input"
                placeholder={m.searchPlaceholder}
                value={search}
                onChange={(e) => handleSearchChange(e.target.value)}
                className={cn('ps-9', search && 'pe-9')}
              />
              {search && (
                <Button
                  id="admin-users-clear-search-btn"
                  type="button"
                  variant="ghost"
                  size="icon-xs"
                  className="absolute top-1/2 end-1 -translate-y-1/2"
                  onClick={() => void setParams({ search: null, page: null })}
                  aria-label={m.clearSearch}
                >
                  <X className="size-3.5" />
                </Button>
              )}
            </div>

            <Select
              value={role ?? 'all'}
              onValueChange={(value) =>
                void setParams({
                  role: value === 'all' ? null : (value as AdminUserRole),
                  page: null,
                })
              }
            >
              <SelectTrigger id="admin-users-role-filter" className="w-[160px]">
                <SelectValue placeholder={m.filterRole} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">{m.filterRoleAll}</SelectItem>
                <SelectItem value="superadmin">{roleLabels.superadmin}</SelectItem>
                <SelectItem value="manager">{roleLabels.manager}</SelectItem>
                <SelectItem value="staff">{roleLabels.staff}</SelectItem>
              </SelectContent>
            </Select>

            <Select
              value={status ?? 'all'}
              onValueChange={(value) =>
                void setParams({
                  status: value === 'all' ? null : (value as 'active' | 'inactive'),
                  page: null,
                })
              }
            >
              <SelectTrigger id="admin-users-status-filter" className="w-[160px]">
                <SelectValue placeholder={m.filterStatus} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">{m.filterStatusAll}</SelectItem>
                <SelectItem value="active">{statusLabels.active}</SelectItem>
                <SelectItem value="inactive">{statusLabels.inactive}</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </ListPageCard.Toolbar>

        <ListPageCard.Content>
          <AdminUserTable
            data={listData?.items ?? []}
            columns={columns}
            isLoading={isListLoading}
            page={page}
            pageSize={pageSize}
            totalPages={listData?.totalPages ?? 1}
            total={listData?.total ?? 0}
            onPageChange={handlePageChange}
            onPageSizeChange={handlePageSizeChange}
            embedded
            showEmptyState={showEmpty}
          />
        </ListPageCard.Content>
      </ListPageCard>

      <TemporaryPasswordDialog
        credential={credential}
        onClose={() => setCredential(null)}
      />
    </div>
  );
}
