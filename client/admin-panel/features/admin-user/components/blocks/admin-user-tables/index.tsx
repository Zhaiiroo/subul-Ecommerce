'use client';

import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
} from '@tanstack/react-table';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { ScrollArea, ScrollBar } from '@/components/ui/scroll-area';
import { Skeleton } from '@/components/ui/skeleton';
import { DataTablePagination } from '@/components/data-table-pagination';
import { messages } from '@/lib/messages.ar';
import { AdminUserEmptyState } from '../admin-user-empty-state';
import type { AdminUserListItem } from '../../../types';

const m = messages.adminUser.listing;

interface AdminUserTableProps {
  data: AdminUserListItem[];
  columns: ColumnDef<AdminUserListItem>[];
  isLoading?: boolean;
  page: number;
  pageSize: number;
  totalPages: number;
  total: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: number) => void;
  embedded?: boolean;
  showEmptyState?: boolean;
}

function SkeletonRows({ columnCount }: { columnCount: number }) {
  return (
    <>
      {Array.from({ length: 8 }).map((_, i) => (
        <TableRow key={i}>
          {Array.from({ length: columnCount }).map((_, j) => (
            <TableCell key={j}>
              <Skeleton className="h-4 w-full" />
            </TableCell>
          ))}
        </TableRow>
      ))}
    </>
  );
}

export function AdminUserTable({
  data,
  columns,
  isLoading = false,
  page,
  pageSize,
  totalPages,
  total,
  onPageChange,
  onPageSizeChange,
  embedded = false,
  showEmptyState = false,
}: AdminUserTableProps) {
  const table = useReactTable({ data, columns, getCoreRowModel: getCoreRowModel() });

  if (showEmptyState) {
    return <AdminUserEmptyState />;
  }

  return (
    <div className="flex flex-1 flex-col gap-3">
      <div className="relative flex flex-1">
        <div
          className={
            embedded
              ? 'absolute inset-0 flex overflow-hidden rounded-lg border border-border/60'
              : 'absolute inset-0 flex overflow-hidden rounded-lg border'
          }
        >
          <ScrollArea className="h-full w-full">
            <Table>
              <TableHeader className="bg-muted sticky top-0 z-10">
                {table.getHeaderGroups().map((hg) => (
                  <TableRow key={hg.id}>
                    {hg.headers.map((h) => (
                      <TableHead key={h.id}>
                        {h.isPlaceholder
                          ? null
                          : flexRender(h.column.columnDef.header, h.getContext())}
                      </TableHead>
                    ))}
                  </TableRow>
                ))}
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  <SkeletonRows columnCount={columns.length} />
                ) : table.getRowModel().rows.length ? (
                  table.getRowModel().rows.map((row) => (
                    <TableRow key={row.id} className="group">
                      {row.getVisibleCells().map((cell) => (
                        <TableCell key={cell.id}>
                          {flexRender(cell.column.columnDef.cell, cell.getContext())}
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : (
                  <TableRow>
                    <TableCell
                      colSpan={columns.length}
                      className="text-muted-foreground h-24 text-center"
                    >
                      {messages.common.noResults}
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
            <ScrollBar orientation="horizontal" />
          </ScrollArea>
        </div>
      </div>

      <DataTablePagination
        page={page}
        pageSize={pageSize}
        totalPages={totalPages}
        total={total}
        isLoading={isLoading}
        onPageChange={onPageChange}
        onPageSizeChange={onPageSizeChange}
        totalLabel={m.paginationTotal}
      />
    </div>
  );
}
